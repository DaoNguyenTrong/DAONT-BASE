import { afterEach, describe, expect, it } from 'vitest'
import {
  applyThemePreference,
  getStoredThemePreference,
  getThemePreference,
  initializeThemePreference,
  setThemePreference,
  THEME_STORAGE_KEY,
  toggleThemePreference,
} from '@/lib/theme'

afterEach(() => {
  document.documentElement.classList.remove('dark')
})

describe('getStoredThemePreference', () => {
  it('returns null when nothing is stored', () => {
    expect(getStoredThemePreference()).toBeNull()
  })

  it('returns the stored value when it is a valid preference', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'dark')
    expect(getStoredThemePreference()).toBe('dark')

    localStorage.setItem(THEME_STORAGE_KEY, 'light')
    expect(getStoredThemePreference()).toBe('light')
  })

  it('returns null when the stored value is garbage', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'blue')
    expect(getStoredThemePreference()).toBeNull()
  })
})

describe('applyThemePreference', () => {
  it('adds the dark class to document.documentElement', () => {
    const result = applyThemePreference('dark')
    expect(document.documentElement.classList.contains('dark')).toBe(true)
    expect(result).toBe('dark')
  })

  it('removes the dark class from document.documentElement', () => {
    document.documentElement.classList.add('dark')
    const result = applyThemePreference('light')
    expect(document.documentElement.classList.contains('dark')).toBe(false)
    expect(result).toBe('light')
  })
})

describe('getThemePreference', () => {
  it('prefers the live DOM class over storage when DOM says dark and storage says light', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'light')
    document.documentElement.classList.add('dark')
    expect(getThemePreference()).toBe('dark')
  })

  it('prefers the live DOM class over storage when DOM says dark and storage is empty', () => {
    document.documentElement.classList.add('dark')
    expect(getThemePreference()).toBe('dark')
  })

  it('falls back to stored preference when DOM has no dark class', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'dark')
    expect(getThemePreference()).toBe('dark')
  })

  it('falls back to light when DOM has no dark class and nothing is stored', () => {
    expect(getThemePreference()).toBe('light')
  })
})

describe('setThemePreference', () => {
  it('persists dark to localStorage and applies the DOM class', () => {
    setThemePreference('dark')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark')
    expect(document.documentElement.classList.contains('dark')).toBe(true)
  })

  it('persists light to localStorage and removes the DOM class', () => {
    document.documentElement.classList.add('dark')
    setThemePreference('light')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('light')
    expect(document.documentElement.classList.contains('dark')).toBe(false)
  })
})

describe('toggleThemePreference', () => {
  it('flips light to dark', () => {
    setThemePreference('light')
    const result = toggleThemePreference()
    expect(result).toBe('dark')
    expect(document.documentElement.classList.contains('dark')).toBe(true)
  })

  it('flips dark to light', () => {
    setThemePreference('dark')
    const result = toggleThemePreference()
    expect(result).toBe('light')
    expect(document.documentElement.classList.contains('dark')).toBe(false)
  })
})

describe('initializeThemePreference', () => {
  it('defaults to light and applies no dark class when nothing is stored', () => {
    const result = initializeThemePreference()
    expect(result).toBe('light')
    expect(document.documentElement.classList.contains('dark')).toBe(false)
  })

  it('applies the dark class when dark is pre-stored', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'dark')
    const result = initializeThemePreference()
    expect(result).toBe('dark')
    expect(document.documentElement.classList.contains('dark')).toBe(true)
  })
})
