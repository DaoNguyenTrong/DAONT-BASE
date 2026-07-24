<script setup lang="ts">
import { darkTheme } from 'naive-ui'
import naiveTheme from '@/theme/naive-theme'
import naiveLocales from '@/locales/naive-ui'

const { isDark } = useThemePreference()
const localeStore = useLocaleStore()
const { currentLocale } = storeToRefs(localeStore)

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
          <RouterView />
        </n-message-provider>
      </n-dialog-provider>
    </n-loading-bar-provider>
  </n-config-provider>
</template>
