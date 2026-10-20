import App from './App.vue'

import './assets/styles/tailwind.css'
import './assets/styles/main.scss'

// createRouter() (hash history) rewrites MSAL's popup "#code=..." to "#/code=...", so the router is
// imported dynamically only after this check. msal-browser 5.x needs the popup itself to broadcast
// the response and close (redirect-bridge `broadcastResponseToMainFrame`), or it hangs.
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

  // Must run before any API call (including restoreSession below) and before any
  // getRuntimeConfig() read: a deploy-time public/config.json overrides the build-time
  // VITE_* values key by key, letting one build serve every environment without a rebuild.
  await loadRuntimeConfig()
  const { apiBaseUrl } = getRuntimeConfig()
  apiClient.defaults.baseURL = apiBaseUrl
  refreshClient.defaults.baseURL = apiBaseUrl

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
