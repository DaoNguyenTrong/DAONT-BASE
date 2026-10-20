export const KEEP_LOGIN_STORAGE_KEY = 'keep-login'

// The auth cookies are HttpOnly on the API's origin, so JS can't see them. This flag records that
// the browser has authenticated before, so a silent refresh on boot is worth trying; without it
// every logged-out visitor pays a guaranteed-401 round-trip that blocks the first paint.
export const SESSION_HINT_STORAGE_KEY = 'has-session'

function canUseStorage() {
  return typeof window !== 'undefined'
}

export function getKeepLoginPreference(fallback = false) {
  if (!canUseStorage()) {
    return fallback
  }

  const value = window.localStorage.getItem(KEEP_LOGIN_STORAGE_KEY)
  if (value === null) {
    return fallback
  }

  return value === 'true'
}

export function setKeepLoginPreference(enabled: boolean) {
  if (!canUseStorage()) {
    return
  }

  if (enabled) {
    window.localStorage.setItem(KEEP_LOGIN_STORAGE_KEY, 'true')
    return
  }

  window.localStorage.removeItem(KEEP_LOGIN_STORAGE_KEY)
}

export function clearKeepLoginPreference() {
  if (!canUseStorage()) {
    return
  }

  window.localStorage.removeItem(KEEP_LOGIN_STORAGE_KEY)
}

export function markSessionHint() {
  if (!canUseStorage()) {
    return
  }

  window.localStorage.setItem(SESSION_HINT_STORAGE_KEY, 'true')
}

export function clearSessionHint() {
  if (!canUseStorage()) {
    return
  }

  window.localStorage.removeItem(SESSION_HINT_STORAGE_KEY)
}

export function hasSessionHint() {
  if (!canUseStorage()) {
    return false
  }

  return window.localStorage.getItem(SESSION_HINT_STORAGE_KEY) === 'true'
}
