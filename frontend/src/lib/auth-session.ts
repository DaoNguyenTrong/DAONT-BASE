export const KEEP_LOGIN_STORAGE_KEY = 'keep-login'

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
