<script setup lang="ts">
import { darkTheme } from 'naive-ui'
import naiveTheme from '@/theme/naive-theme'
import naiveLocales from '@/locales/naive-ui'
import ServerErrorScreen from '@/components/ServerErrorScreen.vue'

const { isDark } = useThemePreference()
const localeStore = useLocaleStore()
const { currentLocale } = storeToRefs(localeStore)

// App-wide API health monitoring — started here (not in the app shell) so the
// server-error takeover also covers the unauthenticated screens (/login etc.).
// /api/health is anonymous, so polling before sign-in is fine.
const health = useHealthStore()
const { isDown } = storeToRefs(health)
const router = useRouter()

onMounted(() => health.startPolling())
onUnmounted(() => health.stopPolling())

// The navigation guard is bypassed while `isDown` (see resolveGuardRedirect), so
// on recovery the router may be parked on a route that no longer matches the
// session state. Re-run the silent refresh and route explicitly once the API
// comes back.
watch(isDown, async (down, wasDown) => {
  if (!wasDown || down) return
  const restored = await restoreSession()
  await router.replace(restored ? useHomeRoute() : { name: 'login' })
})

// A session can lapse while the tab is open (refresh token expired): an API call
// 401s, the client's silent refresh fails, and the auth store clears itself —
// but the guard only runs on navigation, so the user is left on a now-
// unauthorized shell. Bounce to login (keeping the destination) when that
// happens. Boot-time failure never trips this: isAuthenticated starts false, so
// there is no true→false transition.
const { isAuthenticated } = storeToRefs(useAuthStore())
watch(isAuthenticated, (isAuth) => {
  if (isAuth) return
  const current = router.currentRoute.value
  if (!current.meta.requiresAuth) return
  router.replace({ name: 'login', query: { redirect: current.fullPath } })
})

const naiveThemeOverrides = computed(() => (isDark.value ? naiveTheme.dark : naiveTheme.light))
const naiveLocaleConfig = computed(() => naiveLocales[currentLocale.value])
</script>

<template>
  <n-config-provider
    :theme="isDark ? darkTheme : null"
    :theme-overrides="naiveThemeOverrides"
    :locale="naiveLocaleConfig.locale"
    :date-locale="naiveLocaleConfig.dateLocale"
  >
    <n-loading-bar-provider>
      <n-dialog-provider>
        <n-message-provider>
          <ServerErrorScreen v-if="isDown" />
          <RouterView v-else />
        </n-message-provider>
      </n-dialog-provider>
    </n-loading-bar-provider>
  </n-config-provider>
</template>
