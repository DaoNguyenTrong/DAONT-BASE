import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { restoreSession } from '@/composables/use-session-restore'
import { useAuthStore } from '@/stores/auth'
import { KEEP_LOGIN_STORAGE_KEY, setKeepLoginPreference } from '@/lib/auth-session'
import { server } from '../../helpers/msw/server'
import { makeAuthResponse } from '../../helpers/msw/fixtures'
import { setupTestPinia } from '../../helpers/pinia'

describe('restoreSession', () => {
  it('returns true and authenticates the store when refresh succeeds', async () => {
    const pinia = setupTestPinia()
    server.use(
      http.post('*/api/auth/refresh', () => HttpResponse.json(makeAuthResponse())),
    )

    const result = await restoreSession(pinia)
    const auth = useAuthStore(pinia)

    expect(result).toBe(true)
    expect(auth.isAuthenticated).toBe(true)
    expect(auth.account?.username).toBe('test')
  })

  it('returns false, clears keep-login preference, and clears auth when refresh fails with keepLogin true', async () => {
    const pinia = setupTestPinia()
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
    server.use(http.post('*/api/auth/refresh', () => HttpResponse.json(null, { status: 401 })))

    const result = await restoreSession(pinia)
    const auth = useAuthStore(pinia)

    expect(result).toBe(false)
    expect(localStorage.getItem(KEEP_LOGIN_STORAGE_KEY)).toBeNull()
    expect(auth.account).toBeNull()
  })
})
