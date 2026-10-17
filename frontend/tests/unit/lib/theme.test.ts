import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  applyThemePreference,
  getStoredThemePreference,
  getThemePreference,
  initializeThemePreference,
  resolveTheme,
  setThemePreference,
  THEME_STORAGE_KEY,
  toggleThemePreference,
  watchSystemTheme,
} from '@/lib/theme'

afterEach(() => {
  document.documentElement.classList.remove('dark')
  vi.unstubAllGlobals()
})

function stubPrefersDark(initial: boolean) {
  const listeners = new Set<(event: { matches: boolean }) => void>()
  const media = {
    matches: initial,
    addEventListener: (_type: string, listener: (event: { matches: boolean }) => void) =>
      listeners.add(listener),
    removeEventListener: (_type: string, listener: (event: { matches: boolean }) => void) =>
      listeners.delete(listener),
    fire(matches: boolean) {
      media.matches = matches
      listeners.forEach((listener) => listener({ matches }))
    },
  }
  vi.stubGlobal('matchMedia', () => media)
  return media
}

describe('getStoredThemePreference', () => {
  it('returns null when nothing is stored', () => {
    expect(getStoredThemePreference()).toBeNull()
  })

  it('returns the stored value when it is a valid preference', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'dark')
    expect(getStoredThemePreference()).toBe('dark')

    localStorage.setItem(THEME_STORAGE_KEY, 'light')
    expect(getStoredThemePreference()).toBe('light')

    localStorage.setItem(THEME_STORAGE_KEY, 'system')
    expect(getStoredThemePreference()).toBe('system')
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

describe('system preference', () => {
  it('stores system and applies the OS scheme', () => {
    stubPrefersDark(true)
    expect(setThemePreference('system')).toBe('dark')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('system')
    expect(document.documentElement.classList.contains('dark')).toBe(true)
  })

  it('falls back to light when matchMedia is missing', () => {
    vi.stubGlobal('matchMedia', undefined)
    expect(resolveTheme('system')).toBe('light')
  })

  it('follows an OS change only while the setting is system', () => {
    const media = stubPrefersDark(false)
    const seen: string[] = []
    const stop = watchSystemTheme((t) => seen.push(t))
    setThemePreference('system')
    media.fire(true)
    expect(document.documentElement.classList.contains('dark')).toBe(true)
    setThemePreference('light')
    media.fire(false)
    media.fire(true)
    expect(document.documentElement.classList.contains('dark')).toBe(false)
    expect(seen).toEqual(['dark'])
    stop()
  })

  it('toggle from system stores the explicit opposite of what is shown', () => {
    stubPrefersDark(true)
    setThemePreference('system')
    expect(toggleThemePreference()).toBe('light')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('light')
  })

  it('initialize applies a stored system setting', () => {
    stubPrefersDark(true)
    localStorage.setItem(THEME_STORAGE_KEY, 'system')
    expect(initializeThemePreference()).toBe('dark')
  })
})
