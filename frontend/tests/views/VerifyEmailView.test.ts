import { flushPromises } from '@vue/test-utils'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import ResendVerificationForm from '@/components/ResendVerificationForm.vue'
import VerifyEmailView from '@/views/VerifyEmailView.vue'
import { makeAuthResponse } from '../helpers/msw/fixtures'
import { server } from '../helpers/msw/server'
import { renderComponent } from '../helpers/render'

function problemResponse(body: Record<string, unknown>, status: number) {
  return HttpResponse.json(body, {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

describe('VerifyEmailView', () => {
  it('shows a missing-token error and makes no API call when there is no token in the query', async () => {
    let called = false
    server.use(
      http.post('*/api/auth/verify-email', () => {
        called = true
        return HttpResponse.json(makeAuthResponse())
      }),
    )

    const { wrapper } = await renderComponent(VerifyEmailView, { initialRoute: '/verify-email' })
    await flushPromises()

    expect(wrapper.text()).toContain('This verification link is missing or malformed.')
    expect(called).toBe(false)
  })

  it('redirects home after a successful verification', async () => {
    server.use(http.post('*/api/auth/verify-email', () => HttpResponse.json(makeAuthResponse())))

    // VerifyEmailView is mounted standalone here (not behind a <RouterView>), so a
    // successful router.push() moves router.currentRoute but doesn't unmount/replace
    // this component — only the navigation outcome is observable, not a DOM swap.
    const { router } = await renderComponent(VerifyEmailView, {
      initialRoute: '/verify-email?token=abc123',
    })
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('home')
  })

  it('shows an invalid-token error with the resend form for EmailVerificationTokenInvalidOrExpired', async () => {
    server.use(
      http.post('*/api/auth/verify-email', () =>
        problemResponse(
          {
            status: 401,
            title: 'Unauthorized',
            code: 'EmailVerificationTokenInvalidOrExpired',
            detail: 'Expired.',
          },
          401,
        ),
      ),
    )

    const { wrapper, router } = await renderComponent(VerifyEmailView, {
      initialRoute: '/verify-email?token=expired',
    })
    await flushPromises()

    expect(wrapper.text()).toContain('This verification link is invalid or has expired.')
    expect(wrapper.findComponent(ResendVerificationForm).exists()).toBe(true)
    expect(router.currentRoute.value.name).not.toBe('home')
  })

  it('shows the generic fallback message for an unrelated error', async () => {
    server.use(
      http.post('*/api/auth/verify-email', () =>
        problemResponse({ status: 500, title: 'Internal Server Error', code: 'InternalServerError' }, 500),
      ),
    )

    const { wrapper } = await renderComponent(VerifyEmailView, {
      initialRoute: '/verify-email?token=abc123',
    })
    await flushPromises()

    expect(wrapper.text()).toContain('Unable to verify your email right now. Please try again.')
  })
})
