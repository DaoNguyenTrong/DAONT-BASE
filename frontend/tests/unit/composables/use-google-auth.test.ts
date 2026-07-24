import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import type { RouteRecordRaw } from 'vue-router'
import { useGoogleAuth } from '@/composables/use-google-auth'
import { makeAuthResponse } from '../../helpers/msw/fixtures'
import { server } from '../../helpers/msw/server'
import { setupTestPinia } from '../../helpers/pinia'
import { withSetup } from '../../helpers/with-setup'

const ROUTES: RouteRecordRaw[] = [
  { path: '/login', name: 'login', component: { template: '<div />' } },
  { path: '/', name: 'home', component: { template: '<div />' } },
]

function problemResponse(body: Record<string, unknown>, status: number) {
  return HttpResponse.json(body, {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

describe('useGoogleAuth', () => {
  beforeEach(() => {
    setupTestPinia()
  })

  it('redirects home after a successful credential exchange', async () => {
    server.use(http.post('*/api/auth/external/google', () => HttpResponse.json(makeAuthResponse())))

    const { result, app, router } = await withSetup(() => useGoogleAuth(), '/login', ROUTES)
    await result.handleCredential('fake-id-token')

    expect(router.currentRoute.value.name).toBe('home')

    app.unmount()
  })

  it('shows a specific message and offers resend for ExternalLoginEmailNotConfirmed', async () => {
    server.use(
      http.post('*/api/auth/external/google', () =>
        problemResponse(
          { status: 409, title: 'Conflict', code: 'ExternalLoginEmailNotConfirmed', detail: 'Nope.' },
          409,
        ),
      ),
    )

    const { result, app, router } = await withSetup(() => useGoogleAuth(), '/login', ROUTES)
    await result.handleCredential('fake-id-token')

    expect(result.error.value).toBe(
      'An account with this email already exists but has not been verified yet. Verify it below to link Google sign-in.',
    )
    expect(result.showResendVerification.value).toBe(true)
    expect(router.currentRoute.value.name).toBe('login')

    app.unmount()
  })

  it('shows a distinct message for ExternalLoginEmailNotVerifiedByProvider without offering resend', async () => {
    server.use(
      http.post('*/api/auth/external/google', () =>
        problemResponse(
          { status: 401, title: 'Unauthorized', code: 'ExternalLoginEmailNotVerifiedByProvider' },
          401,
        ),
      ),
    )

    const { result, app } = await withSetup(() => useGoogleAuth(), '/login', ROUTES)
    await result.handleCredential('fake-id-token')

    expect(result.error.value).toBe("Google hasn't verified this email address. Please verify it with Google first.")
    expect(result.showResendVerification.value).toBe(false)

    app.unmount()
  })

  it('falls back to a generic message for other codes (e.g. InvalidExternalCredential)', async () => {
    server.use(
      http.post('*/api/auth/external/google', () =>
        problemResponse({ status: 401, title: 'Unauthorized', code: 'InvalidExternalCredential' }, 401),
      ),
    )

    const { result, app } = await withSetup(() => useGoogleAuth(), '/login', ROUTES)
    await result.handleCredential('fake-id-token')

    expect(result.error.value).toBe('Unable to sign in with Google right now. Please try again.')

    app.unmount()
  })
})
