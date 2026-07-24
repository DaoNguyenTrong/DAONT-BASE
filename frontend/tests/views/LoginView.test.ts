import { flushPromises } from '@vue/test-utils'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import GoogleLoginButton from '@/components/GoogleLoginButton.vue'
import ResendVerificationForm from '@/components/ResendVerificationForm.vue'
import LoginView from '@/views/LoginView.vue'
import { server } from '../helpers/msw/server'
import { renderComponent } from '../helpers/render'

function problemResponse(body: Record<string, unknown>, status: number) {
  return HttpResponse.json(body, {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

async function submitLogin(wrapper: Awaited<ReturnType<typeof renderComponent>>['wrapper']) {
  await wrapper.find('input#username').setValue('someuser')
  await wrapper.find('input#password').setValue('somepassword')
  await wrapper.find('form').trigger('submit')
  await flushPromises()
}

describe('LoginView', () => {
  it('renders the Google login button', async () => {
    const { wrapper } = await renderComponent(LoginView)

    expect(wrapper.findComponent(GoogleLoginButton).exists()).toBe(true)
  })

  it('shows a generic message for a plain invalid-credentials 401', async () => {
    server.use(
      http.post('*/api/auth/login', () =>
        problemResponse(
          {
            status: 401,
            title: 'Unauthorized',
            detail: 'Invalid username or password.',
            code: 'InvalidUsernameOrPassword',
          },
          401,
        ),
      ),
    )

    const { wrapper } = await renderComponent(LoginView)
    await submitLogin(wrapper)

    expect(wrapper.text()).toContain('Your username or password is incorrect.')
    expect(wrapper.findComponent(ResendVerificationForm).exists()).toBe(false)
  })

  it('shows the email-not-confirmed message and the resend form when code is EmailNotConfirmed', async () => {
    server.use(
      http.post('*/api/auth/login', () =>
        problemResponse(
          {
            status: 401,
            title: 'Unauthorized',
            detail: 'Email chưa được xác thực.',
            code: 'EmailNotConfirmed',
          },
          401,
        ),
      ),
    )

    const { wrapper } = await renderComponent(LoginView)
    await submitLogin(wrapper)

    expect(wrapper.text()).toContain('Please verify your email address before signing in.')
    expect(wrapper.findComponent(ResendVerificationForm).exists()).toBe(true)
  })

  it('clears the resend form on a fresh submit attempt', async () => {
    server.use(
      http.post('*/api/auth/login', () =>
        problemResponse(
          { status: 401, title: 'Unauthorized', detail: '', code: 'EmailNotConfirmed' },
          401,
        ),
      ),
    )

    const { wrapper } = await renderComponent(LoginView)
    await submitLogin(wrapper)
    expect(wrapper.findComponent(ResendVerificationForm).exists()).toBe(true)

    server.use(
      http.post('*/api/auth/login', () =>
        problemResponse(
          { status: 401, title: 'Unauthorized', detail: '', code: 'InvalidUsernameOrPassword' },
          401,
        ),
      ),
    )
    await submitLogin(wrapper)

    expect(wrapper.findComponent(ResendVerificationForm).exists()).toBe(false)
  })
})
