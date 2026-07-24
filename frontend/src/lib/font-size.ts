export const FONT_SIZE_STORAGE_KEY = 'font-size-preference'
export const DEFAULT_FONT_SIZE = 16
export const MIN_FONT_SIZE = 14
export const MAX_FONT_SIZE = 20

function canUseFontSizeDom() {
  return typeof document !== 'undefined'
}

function canUseFontSizeStorage() {
  return typeof window !== 'undefined'
}

function normalizeFontSize(value: number) {
  return Math.min(MAX_FONT_SIZE, Math.max(MIN_FONT_SIZE, Math.round(value)))
}

export function getStoredFontSize() {
  if (!canUseFontSizeStorage()) {
    return null
  }

  const value = Number(window.localStorage.getItem(FONT_SIZE_STORAGE_KEY))
  return Number.isFinite(value) && value >= MIN_FONT_SIZE && value <= MAX_FONT_SIZE
    ? Math.round(value)
    : null
}

export function applyFontSize(size: number) {
  const normalizedSize = normalizeFontSize(size)

  if (canUseFontSizeDom()) {
    document.documentElement.style.fontSize = `${normalizedSize}px`
  }

  return normalizedSize
}

export function getFontSize() {
  if (canUseFontSizeDom()) {
    const inlineSize = Number.parseFloat(document.documentElement.style.fontSize)
    if (Number.isFinite(inlineSize)) {
      return normalizeFontSize(inlineSize)
    }
  }

  return getStoredFontSize() ?? DEFAULT_FONT_SIZE
}

export function setFontSize(size: number) {
  const normalizedSize = applyFontSize(size)

  if (canUseFontSizeStorage()) {
    window.localStorage.setItem(FONT_SIZE_STORAGE_KEY, String(normalizedSize))
  }

  return normalizedSize
}

export function initializeFontSize() {
  return applyFontSize(getStoredFontSize() ?? DEFAULT_FONT_SIZE)
}
