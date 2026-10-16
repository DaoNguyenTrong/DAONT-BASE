import { describe, expect, it, beforeEach, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { reloadToLogin } from '@/lib/session-exit'
import { server } from '../../helpers/msw/server'
import { useAuthStore } from '@/stores/auth'
import { hasSessionHint } from '@/lib/auth-session'
import { setupTestPinia } from '../../helpers/pinia'
import { makeAuthResponse } from '../../helpers/msw/fixtures'

vi.mock('@/lib/session-exit', () => ({ reloadTo: vi.fn(), reloadToLogin: vi.fn() }))

describe('useAuthStore', () => {
  beforeEach(() => {
    setupTestPinia()
  })

  it('starts unauthenticated', () => {
    const auth = useAuthStore()

    expect(auth.account).toBeNull()
    expect(auth.isAuthenticated).toBe(false)
  })

  it('setAuth authenticates and stores the account', () => {
    const auth = useAuthStore()
    auth.setAuth(makeAuthResponse())

    expect(auth.isAuthenticated).toBe(true)
    expect(auth.account?.username).toBe('test')
  })

  it('clearAuth resets to initial state', () => {
    const auth = useAuthStore()
    auth.setAuth(makeAuthResponse())
    auth.clearAuth()

    expect(auth.account).toBeNull()
    expect(auth.isAuthenticated).toBe(false)
  })

  it('setAuth leaves a session hint for the next boot, clearAuth removes it', () => {
    const auth = useAuthStore()

    auth.setAuth(makeAuthResponse())
    expect(hasSessionHint()).toBe(true)

    auth.clearAuth()
    expect(hasSessionHint()).toBe(false)
  })

  it('setAuth stores the active-org permission set', () => {
    const auth = useAuthStore()
    auth.setAuth({
      ...makeAuthResponse(),
      permissions: ['organizations.members.manage'],
    })

    expect(auth.permissions).toEqual(['organizations.members.manage'])
    expect(auth.hasPermission('organizations.members.manage')).toBe(true)
    expect(auth.hasPermission('organizations.manage')).toBe(false)
  })

  it('clearAuth empties the permission set', () => {
    const auth = useAuthStore()
    auth.setAuth({ ...makeAuthResponse(), permissions: ['organizations.manage'] })
    auth.clearAuth()

    expect(auth.permissions).toEqual([])
    expect(auth.hasPermission('organizations.manage')).toBe(false)
  })

  describe('logout', () => {
    beforeEach(() => {
      vi.mocked(reloadToLogin).mockClear()
    })

    it('clears auth and reloads to a plain login', async () => {
      server.use(http.post('*/api/auth/logout', () => new HttpResponse(null, { status: 204 })))
      const store = useAuthStore()
      store.setAuth(makeAuthResponse())

      await store.logout()

      expect(store.isAuthenticated).toBe(false)
      expect(reloadToLogin).toHaveBeenCalledWith()
    })

    it('still clears auth and reloads when the logout request fails', async () => {
      server.use(http.post('*/api/auth/logout', () => HttpResponse.json({}, { status: 500 })))
      const store = useAuthStore()
      store.setAuth(makeAuthResponse())

      await expect(store.logout()).rejects.toBeDefined()

      expect(store.isAuthenticated).toBe(false)
      expect(reloadToLogin).toHaveBeenCalledWith()
    })
  })
})
