export const THEME_STORAGE_KEY = 'theme-preference'

const DARK_SCHEME_QUERY = '(prefers-color-scheme: dark)'

export type ThemePreference = 'light' | 'dark' | 'system'
export type EffectiveTheme = 'light' | 'dark'

function canUseThemeDom() {
  return typeof document !== 'undefined'
}

function canUseThemeStorage() {
  return typeof window !== 'undefined'
}

function isThemePreference(value: string | null): value is ThemePreference {
  return value === 'light' || value === 'dark' || value === 'system'
}

function getDarkSchemeMedia() {
  return typeof window !== 'undefined' && typeof window.matchMedia === 'function'
    ? window.matchMedia(DARK_SCHEME_QUERY)
    : null
}

export function getStoredThemePreference() {
  if (!canUseThemeStorage()) {
    return null
  }

  const value = window.localStorage.getItem(THEME_STORAGE_KEY)
  return isThemePreference(value) ? value : null
}

export function getThemeSetting(): ThemePreference {
  return getStoredThemePreference() ?? 'light'
}

export function resolveTheme(preference: ThemePreference): EffectiveTheme {
  if (preference !== 'system') {
    return preference
  }

  return getDarkSchemeMedia()?.matches ? 'dark' : 'light'
}

export function applyThemePreference(preference: ThemePreference): EffectiveTheme {
  const theme = resolveTheme(preference)

  if (canUseThemeDom()) {
    document.documentElement.classList.toggle('dark', theme === 'dark')
  }

  return theme
}

export function getThemePreference(): EffectiveTheme {
  if (canUseThemeDom() && document.documentElement.classList.contains('dark')) {
    return 'dark'
  }

  return resolveTheme(getThemeSetting())
}

export function setThemePreference(preference: ThemePreference): EffectiveTheme {
  if (canUseThemeStorage()) {
    window.localStorage.setItem(THEME_STORAGE_KEY, preference)
  }

  return applyThemePreference(preference)
}

export function toggleThemePreference(): EffectiveTheme {
  return setThemePreference(getThemePreference() === 'dark' ? 'light' : 'dark')
}

export function initializeThemePreference(): EffectiveTheme {
  return applyThemePreference(getThemeSetting())
}

export function watchSystemTheme(onChange: (theme: EffectiveTheme) => void): () => void {
  const media = getDarkSchemeMedia()
  if (!media) {
    return () => {}
  }

  const listener = () => {
    if (getThemeSetting() === 'system') {
      onChange(applyThemePreference('system'))
    }
  }
  media.addEventListener('change', listener)

  return () => media.removeEventListener('change', listener)
}
