import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import type { RouteRecordRaw } from 'vue-router'
import { useGoogleAuth } from '@/composables/use-google-auth'
import { makeAuthResponse } from '../../helpers/msw/fixtures'
import { server } from '../../helpers/msw/server'
import { setupTestPinia } from '../../helpers/pinia'
import { withSetup } from '../../helpers/with-setup'
import en from '@/locales/en'

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

  it.each([
    [
      'shows a specific message and offers resend for ExternalLoginEmailNotConfirmed',
      { status: 409, title: 'Conflict', code: 'ExternalLoginEmailNotConfirmed', detail: 'Nope.' },
      en.auth.googleEmailNotConfirmed,
      true,
    ],
    [
      'shows a distinct message for ExternalLoginEmailNotVerifiedByProvider without offering resend',
      { status: 401, title: 'Unauthorized', code: 'ExternalLoginEmailNotVerifiedByProvider' },
      en.auth.googleEmailNotVerifiedByProvider,
      false,
    ],
    [
      'falls back to a generic message for other codes (e.g. InvalidExternalCredential)',
      { status: 401, title: 'Unauthorized', code: 'InvalidExternalCredential' },
      en.auth.googleLoginFailed,
      false,
    ],
  ])('%s', async (_title, problem, message, showResend) => {
    server.use(
      http.post('*/api/auth/external/google', () => problemResponse(problem, problem.status)),
    )

    const { result, app, router } = await withSetup(() => useGoogleAuth(), '/login', ROUTES)
    await result.handleCredential('fake-id-token')

    expect(result.error.value).toBe(message)
    expect(result.showResendVerification.value).toBe(showResend)
    expect(router.currentRoute.value.name).toBe('login')

    app.unmount()
  })
})
