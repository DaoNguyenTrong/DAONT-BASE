import { beforeEach, describe, expect, it } from 'vitest'
import {
  DEFAULT_FONT_SIZE,
  FONT_SIZE_STORAGE_KEY,
  MAX_FONT_SIZE,
  MIN_FONT_SIZE,
  applyFontSize,
  getFontSize,
  getStoredFontSize,
  initializeFontSize,
  setFontSize,
} from '@/lib/font-size'

beforeEach(() => {
  document.documentElement.style.fontSize = ''
})

describe('getStoredFontSize', () => {
  it('returns null when nothing is stored', () => {
    expect(getStoredFontSize()).toBeNull()
  })

  it('returns the rounded value for a valid in-range preference', () => {
    localStorage.setItem(FONT_SIZE_STORAGE_KEY, '15.6')
    expect(getStoredFontSize()).toBe(16)
  })

  it('returns null for an out-of-range value', () => {
    localStorage.setItem(FONT_SIZE_STORAGE_KEY, String(MAX_FONT_SIZE + 5))
    expect(getStoredFontSize()).toBeNull()

    localStorage.setItem(FONT_SIZE_STORAGE_KEY, String(MIN_FONT_SIZE - 5))
    expect(getStoredFontSize()).toBeNull()
  })

  it('returns null for a non-numeric value', () => {
    localStorage.setItem(FONT_SIZE_STORAGE_KEY, 'not-a-number')
    expect(getStoredFontSize()).toBeNull()
  })
})

describe('applyFontSize', () => {
  it('clamps values above the maximum', () => {
    expect(applyFontSize(MAX_FONT_SIZE + 10)).toBe(MAX_FONT_SIZE)
    expect(document.documentElement.style.fontSize).toBe(`${MAX_FONT_SIZE}px`)
  })

  it('clamps values below the minimum', () => {
    expect(applyFontSize(MIN_FONT_SIZE - 10)).toBe(MIN_FONT_SIZE)
    expect(document.documentElement.style.fontSize).toBe(`${MIN_FONT_SIZE}px`)
  })

  it('rounds fractional values before applying', () => {
    expect(applyFontSize(16.7)).toBe(17)
    expect(document.documentElement.style.fontSize).toBe('17px')
  })
})

describe('getFontSize', () => {
  it('prefers the inline document font size when present', () => {
    document.documentElement.style.fontSize = '18px'
    localStorage.setItem(FONT_SIZE_STORAGE_KEY, '16')
    expect(getFontSize()).toBe(18)
  })

  it('falls back to the stored preference when no inline size is set', () => {
    localStorage.setItem(FONT_SIZE_STORAGE_KEY, '15')
    expect(getFontSize()).toBe(15)
  })

  it('falls back to the default when neither inline nor stored size exists', () => {
    expect(getFontSize()).toBe(DEFAULT_FONT_SIZE)
  })
})

describe('setFontSize', () => {
  it('applies and persists the normalized size', () => {
    expect(setFontSize(25)).toBe(MAX_FONT_SIZE)
    expect(document.documentElement.style.fontSize).toBe(`${MAX_FONT_SIZE}px`)
    expect(localStorage.getItem(FONT_SIZE_STORAGE_KEY)).toBe(String(MAX_FONT_SIZE))
  })
})

describe('initializeFontSize', () => {
  it('applies the stored preference when present', () => {
    localStorage.setItem(FONT_SIZE_STORAGE_KEY, '15')
    expect(initializeFontSize()).toBe(15)
    expect(document.documentElement.style.fontSize).toBe('15px')
  })

  it('applies the default when no preference is stored', () => {
    expect(initializeFontSize()).toBe(DEFAULT_FONT_SIZE)
    expect(document.documentElement.style.fontSize).toBe(`${DEFAULT_FONT_SIZE}px`)
  })
})
