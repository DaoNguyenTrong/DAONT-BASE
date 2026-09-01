import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { restoreSession } from '@/composables/use-session-restore'
import { useAuthStore } from '@/stores/auth'
import { useHealthStore } from '@/stores/health'
import { KEEP_LOGIN_STORAGE_KEY, markSessionHint, setKeepLoginPreference } from '@/lib/auth-session'
import { server } from '../../helpers/msw/server'
import { makeAuthResponse } from '../../helpers/msw/fixtures'
import { setupTestPinia } from '../../helpers/pinia'

describe('restoreSession', () => {
  it('skips the refresh call entirely when this browser has no session hint', async () => {
    const pinia = setupTestPinia()
    // No markSessionHint() — and no msw handler, so any request would throw.

    const result = await restoreSession(pinia)
    const auth = useAuthStore(pinia)

    expect(result).toBe(false)
    expect(auth.isAuthenticated).toBe(false)
  })

  it('returns true and authenticates the store when refresh succeeds', async () => {
    const pinia = setupTestPinia()
    markSessionHint()
    server.use(http.post('*/api/auth/refresh', () => HttpResponse.json(makeAuthResponse())))

    const result = await restoreSession(pinia)
    const auth = useAuthStore(pinia)

    expect(result).toBe(true)
    expect(auth.isAuthenticated).toBe(true)
    expect(auth.account?.username).toBe('test')
  })

  it('returns false, clears keep-login preference, and clears auth when refresh fails with keepLogin true', async () => {
    const pinia = setupTestPinia()
    markSessionHint()
    setKeepLoginPreference(true)
    server.use(http.post('*/api/auth/refresh', () => HttpResponse.json(null, { status: 401 })))

    const result = await restoreSession(pinia)
    const auth = useAuthStore(pinia)

    expect(result).toBe(false)
    expect(localStorage.getItem(KEEP_LOGIN_STORAGE_KEY)).toBeNull()
    expect(auth.account).toBeNull()
  })

  it('returns false without touching keep-login preference when refresh fails and keepLogin was false', async () => {
    const pinia = setupTestPinia()
    markSessionHint()
    server.use(http.post('*/api/auth/refresh', () => HttpResponse.json(null, { status: 401 })))

    const result = await restoreSession(pinia)
    const auth = useAuthStore(pinia)

    expect(result).toBe(false)
    expect(localStorage.getItem(KEEP_LOGIN_STORAGE_KEY)).toBeNull()
    expect(auth.account).toBeNull()
  })

  it('a 401 still routes to login — health store is left alone', async () => {
    const pinia = setupTestPinia()
    markSessionHint()
    server.use(http.post('*/api/auth/refresh', () => HttpResponse.json(null, { status: 401 })))

    const result = await restoreSession(pinia)
    const health = useHealthStore(pinia)

    expect(result).toBe(false)
    expect(health.isDown).toBe(false)
  })

  it('marks the health store down (keep-login preserved) when refresh fails with a 5xx', async () => {
    const pinia = setupTestPinia()
    markSessionHint()
    setKeepLoginPreference(true)
    server.use(http.post('*/api/auth/refresh', () => HttpResponse.json(null, { status: 503 })))

    const result = await restoreSession(pinia)
    const health = useHealthStore(pinia)

    expect(result).toBe(false)
    expect(health.isDown).toBe(true)
    // An outage must not log the user out of "keep me signed in".
    expect(localStorage.getItem(KEEP_LOGIN_STORAGE_KEY)).toBe('true')
  })

  it('marks the health store down when refresh fails with a network error (no response)', async () => {
    const pinia = setupTestPinia()
    markSessionHint()
    server.use(http.post('*/api/auth/refresh', () => HttpResponse.error()))

    const result = await restoreSession(pinia)
    const health = useHealthStore(pinia)

    expect(result).toBe(false)
    expect(health.isDown).toBe(true)
  })
})
