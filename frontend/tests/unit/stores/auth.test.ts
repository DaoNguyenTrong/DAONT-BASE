import { describe, expect, it, beforeEach } from 'vitest'
import { useAuthStore } from '@/stores/auth'
import { hasSessionHint } from '@/lib/auth-session'
import { setupTestPinia } from '../../helpers/pinia'
import { makeAuthResponse } from '../../helpers/msw/fixtures'

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
})
