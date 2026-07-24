import { describe, expect, it } from 'vitest'
import {
  clearKeepLoginPreference,
  getKeepLoginPreference,
  KEEP_LOGIN_STORAGE_KEY,
  setKeepLoginPreference,
} from '@/lib/auth-session'

describe('auth-session keep-login preference', () => {
  it('returns false when preference is unset', () => {
    expect(getKeepLoginPreference()).toBe(false)
  })

  it('persists true via setKeepLoginPreference', () => {
    setKeepLoginPreference(true)
    expect(localStorage.getItem(KEEP_LOGIN_STORAGE_KEY)).toBe('true')
    expect(getKeepLoginPreference()).toBe(true)
  })

  it('removes key when setKeepLoginPreference(false)', () => {
    setKeepLoginPreference(true)
    setKeepLoginPreference(false)
    expect(localStorage.getItem(KEEP_LOGIN_STORAGE_KEY)).toBeNull()
  })

  it('removes key via clearKeepLoginPreference', () => {
    setKeepLoginPreference(true)
    clearKeepLoginPreference()
    expect(localStorage.getItem(KEEP_LOGIN_STORAGE_KEY)).toBeNull()
  })

  it('treats non-true stored values as false', () => {
    localStorage.setItem(KEEP_LOGIN_STORAGE_KEY, 'false')
    expect(getKeepLoginPreference()).toBe(false)
  })
})
