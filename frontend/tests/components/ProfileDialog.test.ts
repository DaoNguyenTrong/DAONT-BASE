import { flushPromises } from '@vue/test-utils'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi, afterEach } from 'vitest'
import ProfileDialog from '@/components/ProfileDialog.vue'
import * as feedback from '@/lib/feedback'
import { server } from '../helpers/msw/server'
import { renderComponent } from '../helpers/render'
import { storeIn } from '../helpers/pinia'
import type { ProfileDto, SessionDto } from '@/api/types'
import en from '@/locales/en'
import i18n from '@/plugins/i18n'

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

    expect(placeholderInput(wrapper, en.profile.namePlaceholder).element.value).toBe('Jane Doe')
  })

  it('shows a load-error alert instead of the form when the profile request fails', async () => {
    server.use(
      http.get('*/api/profile', () =>
        problemResponse({ status: 500, title: 'Boom', detail: 'Server error' }, 500),
      ),
    )

    const { wrapper } = await openDialog()

    expect(wrapper.text()).toContain('Server error')
    expect(placeholderInput(wrapper, en.profile.namePlaceholder).exists()).toBe(false)
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

    await placeholderInput(wrapper, en.profile.namePlaceholder).setValue('  Jane Doe  ')
    await placeholderInput(wrapper, en.profile.phonePlaceholder).setValue('  ')
    await placeholderInput(wrapper, en.profile.positionPlaceholder).setValue('')
    await placeholderInput(wrapper, en.profile.addressPlaceholder).setValue('')
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

    expect(placeholderInput(wrapper, en.profile.namePlaceholder).element.value).toBe('New Name')
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
    await placeholderInput(wrapper, en.profile.currentPasswordPlaceholder).setValue('old-pass')
    await placeholderInput(wrapper, en.profile.newPasswordPlaceholder).setValue('new-pass')

    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(successSpy).toHaveBeenCalled()
    expect(placeholderInput(wrapper, en.profile.currentPasswordPlaceholder).element.value).toBe('')
    expect(placeholderInput(wrapper, en.profile.newPasswordPlaceholder).element.value).toBe('')
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
    await placeholderInput(wrapper, en.profile.currentPasswordPlaceholder).setValue('old-pass')
    await placeholderInput(wrapper, en.profile.newPasswordPlaceholder).setValue('new-pass')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain('Current password is incorrect')
    expect(errorSpy).not.toHaveBeenCalled()
  })

  it('shows its own message when the password change is locked, not the backend text', async () => {
    server.use(http.get('*/api/profile', () => HttpResponse.json(makeProfile())))
    server.use(
      http.put('*/api/profile/password', () =>
        problemResponse(
          {
            status: 401,
            title: 'Unauthorized',
            code: 'PasswordChangeTemporarilyLocked',
            detail: 'backend text',
          },
          401,
        ),
      ),
    )

    const { wrapper } = await openDialog()
    await openChangePasswordTab(wrapper)
    await placeholderInput(wrapper, en.profile.currentPasswordPlaceholder).setValue('old-pass')
    await placeholderInput(wrapper, en.profile.newPasswordPlaceholder).setValue('new-pass')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain(i18n.global.t('profile.passwordChangeTemporarilyLocked'))
    expect(wrapper.text()).not.toContain('backend text')
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
    await placeholderInput(wrapper, en.profile.currentPasswordPlaceholder).setValue('old-pass')
    await placeholderInput(wrapper, en.profile.newPasswordPlaceholder).setValue('new-pass')
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
    await placeholderInput(wrapper, en.profile.currentPasswordPlaceholder).setValue('old-pass')
    await placeholderInput(wrapper, en.profile.newPasswordPlaceholder).setValue('new-pass')
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
        deviceInfo:
          'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7; rv:120.0) Gecko/20100101 Firefox/120.0',
      }),
    ])

    const { wrapper } = await openDialog()
    await openSessionsTab(wrapper)

    expect(wrapper.text()).toContain('Chrome on Linux')
    expect(wrapper.text()).toContain('Firefox on macOS')
    expect(wrapper.text()).toContain(en.profile.currentDevice)
  })

  it('shows the empty state when there are no sessions', async () => {
    server.use(http.get('*/api/profile', () => HttpResponse.json(makeProfile())))
    mockSessions([])

    const { wrapper } = await openDialog()
    await openSessionsTab(wrapper)

    expect(wrapper.text()).toContain(en.profile.sessionsEmpty)
  })

  it('does not offer a revoke button for the current session', async () => {
    server.use(http.get('*/api/profile', () => HttpResponse.json(makeProfile())))
    mockSessions([makeSession({ id: 1, isCurrent: true })])

    const { wrapper } = await openDialog()
    await openSessionsTab(wrapper)

    expect(
      wrapper.find(`button[aria-label="${i18n.global.t('profile.signOutDevice')}"]`).exists(),
    ).toBe(false)
  })

  it('revokes a single session and removes it from the list', async () => {
    server.use(http.get('*/api/profile', () => HttpResponse.json(makeProfile())))
    mockSessions([
      makeSession({ id: 1, isCurrent: true }),
      makeSession({
        id: 2,
        isCurrent: false,
        deviceInfo:
          'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7; rv:120.0) Gecko/20100101 Firefox/120.0',
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

    const revokeButtons = wrapper.findAll(
      `button[aria-label="${i18n.global.t('profile.signOutDevice')}"]`,
    )
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
      wrapper.findAll('button').find((b) => b.text().includes(en.profile.signOutOtherDevices)),
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
        deviceInfo:
          'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7; rv:120.0) Gecko/20100101 Firefox/120.0',
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
      .find((b) => b.text().includes(en.profile.signOutOtherDevices))
    await button!.trigger('click')
    await flushPromises()

    expect(revokeOthersCalled).toBe(true)
    expect(wrapper.text()).toContain('Chrome on Linux')
    expect(wrapper.text()).not.toContain('Firefox on macOS')
  })

  describe('avatar', () => {
    function pickFile(wrapper: Awaited<ReturnType<typeof openDialog>>['wrapper'], file: File) {
      const input = wrapper.find<HTMLInputElement>('[data-testid="profile-avatar-input"]')
      Object.defineProperty(input.element, 'files', { value: [file], configurable: true })
      return input.trigger('change')
    }

    function pngFile(size = 10, type = 'image/png') {
      return new File([new Uint8Array(size)], 'me.png', { type })
    }

    it('shows the initials and no remove button when there is no avatar', async () => {
      server.use(http.get('*/api/profile', () => HttpResponse.json(makeProfile())))
      mockSessions([])

      const { wrapper } = await openDialog()

      expect(wrapper.find('[data-testid="profile-avatar"]').text()).toBe('JD')
      expect(wrapper.find('[data-testid="profile-avatar-remove"]').exists()).toBe(false)
    })

    it('shows the avatar image and a remove button when the profile has one', async () => {
      server.use(
        http.get('*/api/profile', () =>
          HttpResponse.json(
            makeProfile({ avatarUrl: 'http://cdn.test/starterkit/public/avatars/a.png' }),
          ),
        ),
      )
      mockSessions([])

      const { wrapper } = await openDialog()

      expect(wrapper.find('[data-testid="profile-avatar"] img').attributes('src')).toBe(
        'http://cdn.test/starterkit/public/avatars/a.png',
      )
      expect(wrapper.find('[data-testid="profile-avatar-remove"]').exists()).toBe(true)
    })

    it('uploads the picked image and shows the returned avatar', async () => {
      server.use(http.get('*/api/profile', () => HttpResponse.json(makeProfile())))
      mockSessions([])
      let uploaded: FormDataEntryValue | null = null
      server.use(
        http.put('*/api/profile/avatar', async ({ request }) => {
          uploaded = (await request.formData()).get('avatar')
          return HttpResponse.json(
            makeProfile({ avatarUrl: 'http://cdn.test/starterkit/public/avatars/new.png' }),
          )
        }),
      )
      const successSpy = vi.spyOn(feedback, 'showSuccessMessage')
      const { wrapper } = await openDialog()

      await pickFile(wrapper, pngFile())
      await flushPromises()

      expect(uploaded).toBeInstanceOf(File)
      expect(wrapper.find('[data-testid="profile-avatar"] img').attributes('src')).toBe(
        'http://cdn.test/starterkit/public/avatars/new.png',
      )
      expect(successSpy).toHaveBeenCalledWith(en.profile.avatarUpdated)
    })

    it('updates the header avatar after an upload and after a removal', async () => {
      server.use(http.get('*/api/profile', () => HttpResponse.json(makeProfile())))
      mockSessions([])
      server.use(
        http.put('*/api/profile/avatar', () =>
          HttpResponse.json(
            makeProfile({ avatarUrl: 'http://cdn.test/starterkit/public/avatars/new.png' }),
          ),
        ),
        http.delete('*/api/profile/avatar', () => new HttpResponse(null, { status: 204 })),
      )
      const { wrapper, pinia } = await openDialog()
      const auth = storeIn(pinia, useAuthStore)
      auth.account = { id: '1', name: 'Jane Doe', username: 'jane' } as typeof auth.account

      await pickFile(wrapper, pngFile())
      await flushPromises()
      expect(auth.account?.avatarUrl).toBe('http://cdn.test/starterkit/public/avatars/new.png')

      await wrapper.get('[data-testid="profile-avatar-remove"]').trigger('click')
      await flushPromises()
      expect(auth.account?.avatarUrl).toBeNull()
    })

    it('rejects an oversized image without calling the server', async () => {
      server.use(http.get('*/api/profile', () => HttpResponse.json(makeProfile())))
      mockSessions([])
      let called = false
      server.use(
        http.put('*/api/profile/avatar', () => {
          called = true
          return HttpResponse.json(makeProfile())
        }),
      )
      const errorSpy = vi.spyOn(feedback, 'showErrorMessage')
      const { wrapper } = await openDialog()

      await pickFile(wrapper, pngFile(2 * 1024 * 1024 + 1))
      await flushPromises()

      expect(called).toBe(false)
      expect(errorSpy).toHaveBeenCalledWith(i18n.global.t('profile.avatarTooLarge', { max: 2 }))
    })

    it('rejects a file that is not a supported image type', async () => {
      server.use(http.get('*/api/profile', () => HttpResponse.json(makeProfile())))
      mockSessions([])
      const errorSpy = vi.spyOn(feedback, 'showErrorMessage')
      const { wrapper } = await openDialog()

      await pickFile(wrapper, pngFile(10, 'image/gif'))
      await flushPromises()

      expect(errorSpy).toHaveBeenCalledWith(en.profile.avatarInvalidType)
    })

    it('reports a server rejection and keeps the current avatar', async () => {
      server.use(http.get('*/api/profile', () => HttpResponse.json(makeProfile())))
      mockSessions([])
      server.use(
        http.put('*/api/profile/avatar', () =>
          problemResponse({ title: 'Bad Request', detail: 'Not an image', status: 400 }, 400),
        ),
      )
      const errorSpy = vi.spyOn(feedback, 'showErrorMessage')
      const { wrapper } = await openDialog()

      await pickFile(wrapper, pngFile())
      await flushPromises()

      expect(errorSpy).toHaveBeenCalledWith('Bad Request', 'Not an image')
      expect(wrapper.find('[data-testid="profile-avatar"]').text()).toBe('JD')
    })

    it('removes the avatar', async () => {
      server.use(
        http.get('*/api/profile', () =>
          HttpResponse.json(
            makeProfile({ avatarUrl: 'http://cdn.test/starterkit/public/avatars/a.png' }),
          ),
        ),
      )
      mockSessions([])
      let deleted = false
      server.use(
        http.delete('*/api/profile/avatar', () => {
          deleted = true
          return HttpResponse.json(makeProfile())
        }),
      )
      const { wrapper } = await openDialog()

      await wrapper.find('[data-testid="profile-avatar-remove"]').trigger('click')
      await flushPromises()

      expect(deleted).toBe(true)
      expect(wrapper.find('[data-testid="profile-avatar"]').text()).toBe('JD')
      expect(wrapper.find('[data-testid="profile-avatar-remove"]').exists()).toBe(false)
    })
  })
})
