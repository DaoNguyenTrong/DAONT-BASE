import App from './App.vue'

import './assets/styles/tailwind.css'
import './assets/styles/main.scss'
import 'virtual:svg-icons-register'

// The app uses hash-based routing (createWebHashHistory), and MSAL's popup sign-in
// also returns its response as a URL hash fragment ("#code=...&client_info=...").
// vue-router normalizes the current hash the moment createWebHashHistory() runs — as
// a side effect of `createRouter()` at router/index.ts's module top level — rewriting
// the unrecognized "#code=..." into "#/code=..." (treating it as an unmatched route).
// A `import router from './router'` at this file's top would trigger that normalization
// on every load (ES module imports evaluate before this file's own body runs), so the
// router module is only ever imported dynamically below, after this check has run.
// A real app route always starts with "#/", so any other hash containing "code=" or
// "error=" is unambiguously an OAuth redirect response, never a route.
//
// It's not enough to just leave the hash alone, though: msal-browser 5.x's popup flow
// no longer polls the popup's location.href from the opener — the popup itself must
// broadcast the response back over a BroadcastChannel (see @azure/msal-browser's
// redirect_bridge/index.mjs, `broadcastResponseToMainFrame`) and close itself. Skipping
// the app mount without calling that bridge left the popup sitting on the callback URL
// forever, since nothing ever posted the response back to the tab that opened it.
function isOAuthPopupRedirect(): boolean {
  const hash = window.location.hash
  return !hash.startsWith('#/') && /[#&](code|error)=/.test(hash)
}

async function handleOAuthPopupRedirect() {
  const { broadcastResponseToMainFrame } = await import('@azure/msal-browser/redirect-bridge')
  await broadcastResponseToMainFrame()
}

async function bootstrap() {
  initializeThemePreference()
  initializeFontSize()

  // Must run before any API call (including restoreSession below): a deploy-time
  // public/config.json overrides the build-time VITE_API_BASE_URL, letting one
  // build target different backends per environment without a rebuild. Falls
  // back to the axios defaults already set from VITE_API_BASE_URL when absent.
  const runtimeConfig = await loadRuntimeConfig()
  if (runtimeConfig.apiBaseUrl) {
    apiClient.defaults.baseURL = runtimeConfig.apiBaseUrl
    refreshClient.defaults.baseURL = runtimeConfig.apiBaseUrl
  }

  const { default: router } = await import('./router')

  const app = createApp(App)
  const pinia = createPinia()

  // Must run before app.use(pinia): restoreSession -> authStore.refreshToken() calls
  // useAuthStore() without an explicit pinia arg, so it relies on this active instance.
  setActivePinia(pinia)
  await restoreSession(pinia)

  app.use(pinia)
  app.use(router)
  app.use(i18n)

  setupErrorHandlers(app)

  app.mount('#app')
}

if (isOAuthPopupRedirect()) {
  void handleOAuthPopupRedirect()
} else {
  void bootstrap()
}
