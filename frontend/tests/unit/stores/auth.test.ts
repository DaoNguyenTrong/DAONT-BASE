import { describe, expect, it, beforeEach } from 'vitest'
import { useAuthStore } from '@/stores/auth'
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
})
