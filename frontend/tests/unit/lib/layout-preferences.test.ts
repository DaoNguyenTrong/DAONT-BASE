import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
  LAYOUT_STORAGE_KEYS,
  LEGACY_SIDEBAR_MODE_KEY,
  readLayoutPreferences,
  writeLayoutPreference,
} from '@/lib/layout-preferences'

const DEFAULTS = {
  menuMode: 'static',
  menuTheme: 'brand',
  drawerPinned: false,
  staticInactive: false,
}

describe('readLayoutPreferences', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('returns the defaults when nothing is stored', () => {
    expect(readLayoutPreferences()).toEqual(DEFAULTS)
  })

  it('reads stored values', () => {
    localStorage.setItem(LAYOUT_STORAGE_KEYS.menuMode, 'horizontal')
    localStorage.setItem(LAYOUT_STORAGE_KEYS.menuTheme, 'transparent')
    localStorage.setItem(LAYOUT_STORAGE_KEYS.drawerPinned, 'true')
    localStorage.setItem(LAYOUT_STORAGE_KEYS.staticInactive, 'true')

    expect(readLayoutPreferences()).toEqual({
      menuMode: 'horizontal',
      menuTheme: 'transparent',
      drawerPinned: true,
      staticInactive: true,
    })
  })

  it.each([
    ['full', 'static'],
    ['minimal', 'slim'],
  ])('migrates legacy sidebar-mode %s to %s', (legacy, expected) => {
    localStorage.setItem(LEGACY_SIDEBAR_MODE_KEY, legacy)

    expect(readLayoutPreferences().menuMode).toBe(expected)
    expect(localStorage.getItem(LAYOUT_STORAGE_KEYS.menuMode)).toBe(expected)
    expect(localStorage.getItem(LEGACY_SIDEBAR_MODE_KEY)).toBeNull()
  })

  it('ignores the legacy key once the new key exists', () => {
    localStorage.setItem(LAYOUT_STORAGE_KEYS.menuMode, 'slim-plus')
    localStorage.setItem(LEGACY_SIDEBAR_MODE_KEY, 'full')

    expect(readLayoutPreferences().menuMode).toBe('slim-plus')
  })

  it('falls back to the default for garbage values', () => {
    localStorage.setItem(LAYOUT_STORAGE_KEYS.menuMode, 'sideways')
    localStorage.setItem(LAYOUT_STORAGE_KEYS.menuTheme, 'neon')
    localStorage.setItem(LAYOUT_STORAGE_KEYS.drawerPinned, 'yes')
    localStorage.setItem(LAYOUT_STORAGE_KEYS.staticInactive, 'yes')

    expect(readLayoutPreferences()).toEqual(DEFAULTS)
  })

  it('returns defaults when storage throws', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked')
    })

    expect(readLayoutPreferences()).toEqual(DEFAULTS)
  })
})

describe('writeLayoutPreference', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('stores strings as-is and booleans as true/false', () => {
    writeLayoutPreference('menuMode', 'slim')
    writeLayoutPreference('drawerPinned', true)

    expect(localStorage.getItem(LAYOUT_STORAGE_KEYS.menuMode)).toBe('slim')
    expect(localStorage.getItem(LAYOUT_STORAGE_KEYS.drawerPinned)).toBe('true')
  })

  it('does not throw when storage throws', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked')
    })

    expect(() => writeLayoutPreference('menuMode', 'slim')).not.toThrow()
  })
})
