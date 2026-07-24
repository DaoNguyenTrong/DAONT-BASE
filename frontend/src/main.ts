import App from './App.vue'
import router from './router'

import './assets/styles/tailwind.css'
import './assets/styles/main.scss'
import 'virtual:svg-icons-register'

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

  const app = createApp(App)
  const pinia = createPinia()

  // Must run before app.use(pinia): restoreSession -> authApi calls useAuthStore()
  // without an explicit pinia arg, so it relies on this active instance.
  setActivePinia(pinia)
  await restoreSession(pinia)

  app.use(pinia)
  app.use(router)
  app.use(i18n)

  setupErrorHandlers(app)

  app.mount('#app')
}

void bootstrap()
