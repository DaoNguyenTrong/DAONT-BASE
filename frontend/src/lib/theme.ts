export const THEME_STORAGE_KEY = 'theme-preference'

export type ThemePreference = 'light' | 'dark'

function canUseThemeDom() {
  return typeof document !== 'undefined'
}

function canUseThemeStorage() {
  return typeof window !== 'undefined'
}

function isThemePreference(value: string | null): value is ThemePreference {
  return value === 'light' || value === 'dark'
}

export function getStoredThemePreference() {
  if (!canUseThemeStorage()) {
    return null
  }

  const value = window.localStorage.getItem(THEME_STORAGE_KEY)
  return isThemePreference(value) ? value : null
}

export function applyThemePreference(theme: ThemePreference) {
  if (canUseThemeDom()) {
    document.documentElement.classList.toggle('dark', theme === 'dark')
  }

  return theme
}

export function getThemePreference() {
  if (canUseThemeDom() && document.documentElement.classList.contains('dark')) {
    return 'dark' as const
  }

  return getStoredThemePreference() ?? 'light'
}

export function setThemePreference(theme: ThemePreference) {
  if (canUseThemeStorage()) {
    window.localStorage.setItem(THEME_STORAGE_KEY, theme)
  }

  return applyThemePreference(theme)
}

export function toggleThemePreference() {
  return setThemePreference(getThemePreference() === 'dark' ? 'light' : 'dark')
}

export function initializeThemePreference() {
  return applyThemePreference(getStoredThemePreference() ?? 'light')
}
