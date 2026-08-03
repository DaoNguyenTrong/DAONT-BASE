import { flushPromises } from '@vue/test-utils'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi, afterEach } from 'vitest'
import ProfileDialog from '@/components/ProfileDialog.vue'
import * as feedback from '@/lib/feedback'
import { server } from '../helpers/msw/server'
import { renderComponent } from '../helpers/render'
import type { ProfileDto, SessionDto } from '@/api/types'

function makeProfile(overrides: Partial<ProfileDto> = {}): ProfileDto {
  return {
    id: 'p1',
    name: 'Jane Doe',
    username: 'jane',
    email: 'jane@example.com',
    emailConfirmed: true,
    phone: '0900000000',
    position: 'Engineer',
    address: '123 Main St',
    hasPassword: true,
    ...overrides,
  }
}

function makeSession(overrides: Partial<SessionDto> = {}): SessionDto {
  return {
    id: 1,
    deviceInfo: 'Chrome on Windows',
    ipAddress: '127.0.0.1',
    isPersistent: false,
    isCurrent: false,
    loginAt: '2026-01-01T00:00:00Z',
    lastActiveAt: '2026-01-01T12:00:00Z',
    expiresAt: '2026-02-01T00:00:00Z',
    ...overrides,
  }
}

function mockSessions(sessions: SessionDto[]) {
  server.use(http.get('*/api/auth/sessions', () => HttpResponse.json(sessions)))
}

function problemResponse(body: Record<string, unknown>, status: number) {
  return HttpResponse.json(body, {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

async function openDialog() {
  const result = await renderComponent(ProfileDialog, {
    props: { visible: false },
    global: { stubs: { teleport: true } },
  })
  await result.wrapper.setProps({ visible: true })
  await flushPromises()
  return result
}

function placeholderInput(
  wrapper: Awaited<ReturnType<typeof openDialog>>['wrapper'],
  placeholder: string,
) {
  return wrapper.find<HTMLInputElement>(`input[placeholder="${placeholder}"]`)
}

async function openChangePasswordTab(wrapper: Awaited<ReturnType<typeof openDialog>>['wrapper']) {
  await wrapper.find('[data-name="changePassword"]').trigger('click')
  await flushPromises()
}

async function openSessionsTab(wrapper: Awaited<ReturnType<typeof openDialog>>['wrapper']) {
  await wrapper.find('[data-name="sessions"]').trigger('click')
  await flushPromises()
}

describe('ProfileDialog', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('shows a loading skeleton while the profile request is in flight', async () => {
    let release!: () => void
    const gate = new Promise<void>((resolve) => {
      release = resolve
    })
    server.use(
      http.get('*/api/profile', async () => {
        await gate
        return HttpResponse.json(makeProfile())
      }),
    )

    const { wrapper } = await renderComponent(ProfileDialog, {
      props: { visible: false },
      global: { stubs: { teleport: true } },
    })
    await wrapper.setProps({ visible: true })
    await flushPromises()

    expect(wrapper.findAllComponents({ name: 'Skeleton' }).length).toBeGreaterThan(0)

    release()
    await flushPromises()
    expect(wrapper.findAllComponents({ name: 'Skeleton' }).length).toBe(0)
  })

  it('populates the form from the loaded profile', async () => {
    server.use(
      http.get('*/api/profile', () => HttpResponse.json(makeProfile({ name: 'Jane Doe' }))),
    )

    const { wrapper } = await openDialog()

    expect(placeholderInput(wrapper, 'Enter the full name').element.value).toBe('Jane Doe')
  })

  it('shows a load-error alert instead of the form when the profile request fails', async () => {
    server.use(
      http.get('*/api/profile', () =>
        problemResponse({ status: 500, title: 'Boom', detail: 'Server error' }, 500),
      ),
    )

    const { wrapper } = await openDialog()

    expect(wrapper.text()).toContain('Server error')
    expect(placeholderInput(wrapper, 'Enter the full name').exists()).toBe(false)
  })

  it('trims strings and sends null for empty optional fields on update', async () => {
    server.use(http.get('*/api/profile', () => HttpResponse.json(makeProfile())))
    let requestBody: Record<string, unknown> | undefined
    server.use(
      http.put('*/api/profile', async ({ request }) => {
        requestBody = (await request.json()) as Record<string, unknown>
        return HttpResponse.json(makeProfile())
      }),
    )

    const { wrapper } = await openDialog()

    await placeholderInput(wrapper, 'Enter the full name').setValue('  Jane Doe  ')
    await placeholderInput(wrapper, 'Enter the phone number').setValue('  ')
    await placeholderInput(wrapper, 'Enter the position').setValue('')
    await placeholderInput(wrapper, 'Enter the address').setValue('')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(requestBody?.name).toBe('Jane Doe')
    expect(requestBody?.phone).toBeNull()
    expect(requestBody?.position).toBeNull()
    expect(requestBody?.address).toBeNull()
  })

  it('re-fills the form and shows a success toast when the update succeeds', async () => {
    server.use(
      http.get('*/api/profile', () => HttpResponse.json(makeProfile({ name: 'Old Name' }))),
    )
    server.use(
      http.put('*/api/profile', () => HttpResponse.json(makeProfile({ name: 'New Name' }))),
    )
    const successSpy = vi.spyOn(feedback, 'showSuccessMessage')

    const { wrapper } = await openDialog()
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(placeholderInput(wrapper, 'Enter the full name').element.value).toBe('New Name')
    expect(successSpy).toHaveBeenCalled()
  })

  it('maps 422 validation errors onto the matching profile field', async () => {
    server.use(http.get('*/api/profile', () => HttpResponse.json(makeProfile())))
    server.use(
      http.put('*/api/profile', () =>
        problemResponse(
          { status: 422, title: 'Validation failed', errors: { Name: ['Name is required'] } },
          422,
        ),
      ),
    )

    const { wrapper } = await openDialog()
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain('Name is required')
  })

  it('shows an error toast without field errors for a non-validation update failure', async () => {
    server.use(http.get('*/api/profile', () => HttpResponse.json(makeProfile())))
    server.use(
      http.put('*/api/profile', () => problemResponse({ status: 500, title: 'Server error' }, 500)),
    )
    const errorSpy = vi.spyOn(feedback, 'showErrorMessage')

    const { wrapper } = await openDialog()
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(errorSpy).toHaveBeenCalled()
    expect(wrapper.text()).not.toContain('Name is required')
  })

  it('clears the password fields and shows a success toast on a successful password change', async () => {
    server.use(http.get('*/api/profile', () => HttpResponse.json(makeProfile())))
    server.use(http.put('*/api/profile/password', () => new HttpResponse(null, { status: 204 })))
    const successSpy = vi.spyOn(feedback, 'showSuccessMessage')

    const { wrapper } = await openDialog()
    await openChangePasswordTab(wrapper)
    await placeholderInput(wrapper, 'Enter your current password').setValue('old-pass')
    await placeholderInput(wrapper, 'Enter a new password').setValue('new-pass')

    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(successSpy).toHaveBeenCalled()
    expect(placeholderInput(wrapper, 'Enter your current password').element.value).toBe('')
    expect(placeholderInput(wrapper, 'Enter a new password').element.value).toBe('')
  })

  it('maps a 401 password-change failure to the current-password field specifically', async () => {
    server.use(http.get('*/api/profile', () => HttpResponse.json(makeProfile())))
    server.use(
      http.put('*/api/profile/password', () =>
        problemResponse(
          { status: 401, title: 'Unauthorized', detail: 'Current password is incorrect' },
          401,
        ),
      ),
    )
    const errorSpy = vi.spyOn(feedback, 'showErrorMessage')

    const { wrapper } = await openDialog()
    await openChangePasswordTab(wrapper)
    await placeholderInput(wrapper, 'Enter your current password').setValue('old-pass')
    await placeholderInput(wrapper, 'Enter a new password').setValue('new-pass')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain('Current password is incorrect')
    expect(errorSpy).not.toHaveBeenCalled()
  })

  it('maps other password-change validation errors the same way as the profile form', async () => {
    server.use(http.get('*/api/profile', () => HttpResponse.json(makeProfile())))
    server.use(
      http.put('*/api/profile/password', () =>
        problemResponse(
          { status: 422, title: 'Validation failed', errors: { NewPassword: ['Too short'] } },
          422,
        ),
      ),
    )

    const { wrapper } = await openDialog()
    await openChangePasswordTab(wrapper)
    await placeholderInput(wrapper, 'Enter your current password').setValue('old-pass')
    await placeholderInput(wrapper, 'Enter a new password').setValue('new-pass')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain('Too short')
  })

  it('shows an error toast for a non-401 password-change failure without field errors', async () => {
    server.use(http.get('*/api/profile', () => HttpResponse.json(makeProfile())))
    server.use(
      http.put('*/api/profile/password', () =>
        problemResponse({ status: 500, title: 'Server error' }, 500),
      ),
    )
    const errorSpy = vi.spyOn(feedback, 'showErrorMessage')

    const { wrapper } = await openDialog()
    await openChangePasswordTab(wrapper)
    await placeholderInput(wrapper, 'Enter your current password').setValue('old-pass')
    await placeholderInput(wrapper, 'Enter a new password').setValue('new-pass')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(errorSpy).toHaveBeenCalled()
  })

  it('hides the change-password tab for an account with no password set', async () => {
    server.use(
      http.get('*/api/profile', () => HttpResponse.json(makeProfile({ hasPassword: false }))),
    )

    const { wrapper } = await openDialog()

    expect(wrapper.find('[data-name="changePassword"]').exists()).toBe(false)
  })

  // Sessions tab

  it('renders active sessions and tags the current device', async () => {
    server.use(http.get('*/api/profile', () => HttpResponse.json(makeProfile())))
    mockSessions([
      makeSession({
        id: 1,
        isCurrent: true,
        deviceInfo:
          'Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
      }),
      makeSession({
        id: 2,
        isCurrent: false,
        deviceInfo: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7; rv:120.0) Gecko/20100101 Firefox/120.0',
      }),
    ])

    const { wrapper } = await openDialog()
    await openSessionsTab(wrapper)

    expect(wrapper.text()).toContain('Chrome on Linux')
    expect(wrapper.text()).toContain('Firefox on macOS')
    expect(wrapper.text()).toContain('This device')
  })

  it('shows the empty state when there are no sessions', async () => {
    server.use(http.get('*/api/profile', () => HttpResponse.json(makeProfile())))
    mockSessions([])

    const { wrapper } = await openDialog()
    await openSessionsTab(wrapper)

    expect(wrapper.text()).toContain('No active sessions.')
  })

  it('does not offer a revoke button for the current session', async () => {
    server.use(http.get('*/api/profile', () => HttpResponse.json(makeProfile())))
    mockSessions([makeSession({ id: 1, isCurrent: true })])

    const { wrapper } = await openDialog()
    await openSessionsTab(wrapper)

    expect(wrapper.find('button[aria-label="Sign out"]').exists()).toBe(false)
  })

  it('revokes a single session and removes it from the list', async () => {
    server.use(http.get('*/api/profile', () => HttpResponse.json(makeProfile())))
    mockSessions([
      makeSession({ id: 1, isCurrent: true }),
      makeSession({
        id: 2,
        isCurrent: false,
        deviceInfo: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7; rv:120.0) Gecko/20100101 Firefox/120.0',
      }),
    ])
    let revokedId: string | undefined
    server.use(
      http.delete('*/api/auth/sessions/:id', ({ params }) => {
        revokedId = params.id as string
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const { wrapper } = await openDialog()
    await openSessionsTab(wrapper)

    const revokeButtons = wrapper.findAll('button[aria-label="Sign out"]')
    expect(revokeButtons).toHaveLength(1)
    await revokeButtons[0]!.trigger('click')
    await flushPromises()

    expect(revokedId).toBe('2')
    expect(wrapper.text()).not.toContain('Firefox on macOS')
  })

  it('hides the "sign out of other devices" button when there is nothing else to revoke', async () => {
    server.use(http.get('*/api/profile', () => HttpResponse.json(makeProfile())))
    mockSessions([makeSession({ id: 1, isCurrent: true })])

    const { wrapper } = await openDialog()
    await openSessionsTab(wrapper)

    expect(
      wrapper.findAll('button').find((b) => b.text().includes('Sign out of other devices')),
    ).toBeUndefined()
  })

  it('revokes other sessions and keeps only the current one', async () => {
    server.use(http.get('*/api/profile', () => HttpResponse.json(makeProfile())))
    mockSessions([
      makeSession({
        id: 1,
        isCurrent: true,
        deviceInfo:
          'Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
      }),
      makeSession({
        id: 2,
        isCurrent: false,
        deviceInfo: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7; rv:120.0) Gecko/20100101 Firefox/120.0',
      }),
    ])
    let revokeOthersCalled = false
    server.use(
      http.post('*/api/auth/sessions/revoke-others', () => {
        revokeOthersCalled = true
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const { wrapper } = await openDialog()
    await openSessionsTab(wrapper)

    const button = wrapper
      .findAll('button')
      .find((b) => b.text().includes('Sign out of other devices'))
    await button!.trigger('click')
    await flushPromises()

    expect(revokeOthersCalled).toBe(true)
    expect(wrapper.text()).toContain('Chrome on Linux')
    expect(wrapper.text()).not.toContain('Firefox on macOS')
  })
})
