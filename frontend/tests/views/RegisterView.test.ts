import { flushPromises } from '@vue/test-utils'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import GoogleLoginButton from '@/components/GoogleLoginButton.vue'
import ResendVerificationForm from '@/components/ResendVerificationForm.vue'
import RegisterView from '@/views/RegisterView.vue'
import { server } from '../helpers/msw/server'
import { renderComponent } from '../helpers/render'

function problemResponse(body: Record<string, unknown>, status: number) {
  return HttpResponse.json(body, {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

async function fillValidForm(wrapper: Awaited<ReturnType<typeof renderComponent>>['wrapper']) {
  await wrapper.find('input#name').setValue('Nguyen Van A')
  await wrapper.find('input#username').setValue('nguyenvana')
  await wrapper.find('input#email').setValue('nguyenvana@example.com')
  await wrapper.find('input#password').setValue('Password123!')
}

async function submitRegister(wrapper: Awaited<ReturnType<typeof renderComponent>>['wrapper']) {
  await wrapper.find('form').trigger('submit')
  await flushPromises()
}

describe('RegisterView', () => {
  it('renders the Google login button alongside the form', async () => {
    const { wrapper } = await renderComponent(RegisterView)

    expect(wrapper.findComponent(GoogleLoginButton).exists()).toBe(true)
  })

  it('shows required errors for every field on an empty submit and makes no API call', async () => {
    let called = false
    server.use(
      http.post('*/api/auth/register', () => {
        called = true
        return HttpResponse.json({ accountId: 'a1', email: 'x@example.com' }, { status: 202 })
      }),
    )

    const { wrapper } = await renderComponent(RegisterView)
    await submitRegister(wrapper)

    expect(wrapper.text()).toContain('Name is required.')
    expect(wrapper.text()).toContain('Username is required.')
    expect(wrapper.text()).toContain('Email is required.')
    expect(wrapper.text()).toContain('Password is required.')
    expect(called).toBe(false)
  })

  it('shows passwordTooShort for a short password without calling the API', async () => {
    let called = false
    server.use(
      http.post('*/api/auth/register', () => {
        called = true
        return HttpResponse.json({ accountId: 'a1', email: 'x@example.com' }, { status: 202 })
      }),
    )

    const { wrapper } = await renderComponent(RegisterView)
    await fillValidForm(wrapper)
    await wrapper.find('input#password').setValue('short')
    await submitRegister(wrapper)

    expect(wrapper.text()).toContain('Password must be at least 8 characters.')
    expect(called).toBe(false)
  })

  it('swaps to the success panel with the registered email on 202', async () => {
    server.use(
      http.post('*/api/auth/register', () =>
        HttpResponse.json({ accountId: 'a1', email: 'nguyenvana@example.com' }, { status: 202 }),
      ),
    )

    const { wrapper } = await renderComponent(RegisterView)
    await fillValidForm(wrapper)
    await submitRegister(wrapper)

    expect(wrapper.find('input#username').exists()).toBe(false)
    expect(wrapper.text()).toContain('nguyenvana@example.com')
    expect(wrapper.findComponent(ResendVerificationForm).exists()).toBe(true)
    expect(wrapper.findComponent(GoogleLoginButton).exists()).toBe(false)
  })

  it('maps a 400 ValidationFailed response onto the matching field', async () => {
    server.use(
      http.post('*/api/auth/register', () =>
        problemResponse(
          { status: 400, title: 'Bad Request', code: 'ValidationFailed', errors: { email: ['Bad email'] } },
          400,
        ),
      ),
    )

    const { wrapper } = await renderComponent(RegisterView)
    await fillValidForm(wrapper)
    await submitRegister(wrapper)

    expect(wrapper.text()).toContain('Bad email')
    expect(wrapper.find('form').exists()).toBe(true)
  })

  it('attaches AccountUsernameAlreadyExists to the username field, not a top-level alert', async () => {
    server.use(
      http.post('*/api/auth/register', () =>
        problemResponse(
          { status: 409, title: 'Conflict', code: 'AccountUsernameAlreadyExists', detail: 'Taken.' },
          409,
        ),
      ),
    )

    const { wrapper } = await renderComponent(RegisterView)
    await fillValidForm(wrapper)
    await submitRegister(wrapper)

    expect(wrapper.text()).toContain('Taken.')
    expect(wrapper.find('form').exists()).toBe(true)
  })

  it('attaches AccountEmailAlreadyExists to the email field', async () => {
    server.use(
      http.post('*/api/auth/register', () =>
        problemResponse(
          { status: 409, title: 'Conflict', code: 'AccountEmailAlreadyExists', detail: 'Email taken.' },
          409,
        ),
      ),
    )

    const { wrapper } = await renderComponent(RegisterView)
    await fillValidForm(wrapper)
    await submitRegister(wrapper)

    expect(wrapper.text()).toContain('Email taken.')
  })

  it('clears a field server error when the user edits that field', async () => {
    server.use(
      http.post('*/api/auth/register', () =>
        problemResponse(
          { status: 409, title: 'Conflict', code: 'AccountUsernameAlreadyExists', detail: 'Taken.' },
          409,
        ),
      ),
    )

    const { wrapper } = await renderComponent(RegisterView)
    await fillValidForm(wrapper)
    await submitRegister(wrapper)
    expect(wrapper.text()).toContain('Taken.')

    await wrapper.find('input#username').setValue('anotherusername')

    expect(wrapper.text()).not.toContain('Taken.')
  })

  it('is not permanently locked after a 409 — fixing the field allows a successful resubmit', async () => {
    let attempt = 0
    server.use(
      http.post('*/api/auth/register', () => {
        attempt += 1
        if (attempt === 1) {
          return problemResponse(
            { status: 409, title: 'Conflict', code: 'AccountUsernameAlreadyExists', detail: 'Taken.' },
            409,
          )
        }
        return HttpResponse.json({ accountId: 'a1', email: 'nguyenvana@example.com' }, { status: 202 })
      }),
    )

    const { wrapper } = await renderComponent(RegisterView)
    await fillValidForm(wrapper)
    await submitRegister(wrapper)
    expect(wrapper.text()).toContain('Taken.')

    await wrapper.find('input#username').setValue('anotherusername')
    await submitRegister(wrapper)

    expect(wrapper.find('input#username').exists()).toBe(false)
    expect(wrapper.text()).toContain('nguyenvana@example.com')
  })
})
