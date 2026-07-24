import type { Pinia } from 'pinia'

export async function restoreSession(pinia: Pinia) {
  const authStore = useAuthStore(pinia)
  const keepLogin = getKeepLoginPreference()

  try {
    await authStore.refreshToken()
    return true
  } catch {
    if (keepLogin) {
      clearKeepLoginPreference()
    }
    authStore.clearAuth()
    return false
  }
}
