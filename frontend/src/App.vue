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

// The guard is bypassed while `isDown` (see resolveGuardRedirect), so on recovery the current
// route may not match the session state: re-run the silent refresh and route explicitly.
watch(isDown, async (down, wasDown) => {
  if (!wasDown || down) return
  const restored = await restoreSession()
  await router.replace(restored ? useHomeRoute() : { name: 'login' })
})

// The guard only runs on navigation, so a session lapsing mid-tab (silent refresh fails) reloads
// to login here — a full reload, so no store keeps the lapsed account's data. Never fires on boot
// (no true→false flip); on logout, the logout's own reload already won and this is a no-op.
const { isAuthenticated } = storeToRefs(useAuthStore())
watch(isAuthenticated, (isAuth) => {
  if (isAuth) return
  const current = router.currentRoute.value
  if (!current.meta.requiresAuth) return
  reloadToLogin({ redirect: current.fullPath })
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
