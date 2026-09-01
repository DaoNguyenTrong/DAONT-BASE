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
    // The API is unreachable — not "no valid session". Don't wipe the keep-login
    // preference or fall through to the /login redirect over an outage: hand the
    // signal to the health store so App.vue renders ServerErrorScreen on the
    // first paint (no login flash), and its poll recovers the app when the API
    // comes back.
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
