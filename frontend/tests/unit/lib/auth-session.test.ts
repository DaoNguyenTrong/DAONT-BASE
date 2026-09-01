import { describe, expect, it } from 'vitest'
import {
  clearKeepLoginPreference,
  clearSessionHint,
  getKeepLoginPreference,
  hasSessionHint,
  KEEP_LOGIN_STORAGE_KEY,
  markSessionHint,
  SESSION_HINT_STORAGE_KEY,
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

describe('auth-session session hint', () => {
  it('is absent by default', () => {
    expect(hasSessionHint()).toBe(false)
  })

  it('persists via markSessionHint and reads back true', () => {
    markSessionHint()
    expect(localStorage.getItem(SESSION_HINT_STORAGE_KEY)).toBe('true')
    expect(hasSessionHint()).toBe(true)
  })

  it('is removed via clearSessionHint', () => {
    markSessionHint()
    clearSessionHint()
    expect(localStorage.getItem(SESSION_HINT_STORAGE_KEY)).toBeNull()
    expect(hasSessionHint()).toBe(false)
  })
})
