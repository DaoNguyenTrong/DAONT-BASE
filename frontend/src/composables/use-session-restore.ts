import type { Pinia } from 'pinia'

// `pinia` is explicit on app boot (called from main.ts before app.use(pinia));
// omit it when calling from inside a component (App.vue's recovery watcher),
// where the active pinia is already installed.
export async function restoreSession(pinia?: Pinia) {
  const authStore = useAuthStore(pinia)
  const healthStore = useHealthStore(pinia)
  const keepLogin = getKeepLoginPreference()

  // No prior authentication on this browser → there is no refresh cookie to trade
  // in. Skip the guaranteed-401 round-trip so it doesn't block the first paint for
  // a logged-out visitor.
  if (!hasSessionHint()) {
    return false
  }

  try {
    await authStore.refreshToken()
    return true
  } catch (error) {
    // An outage isn't "no valid session": keep the keep-login preference and skip the /login
    // redirect; the health store shows ServerErrorScreen on first paint and its poll recovers.
    if (isServerOutage(error)) {
      healthStore.reportOutage()
      return false
    }

    if (keepLogin) {
      clearKeepLoginPreference()
    }
    authStore.clearAuth()
    return false
  }
}
