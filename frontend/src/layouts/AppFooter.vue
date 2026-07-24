<script setup lang="ts">
const { t, locale } = useI18n()
const version = __APP_VERSION__
const { status, apiVersion, apiBuildTimestamp } = useHealthStatus()

const formattedBuildTime = computed(() => {
  if (!apiBuildTimestamp.value) return null
  return new Intl.DateTimeFormat(locale.value, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(apiBuildTimestamp.value))
})

const statusDotClass = computed(() => {
  if (status.value === 'online') return 'bg-green-500'
  if (status.value === 'offline') return 'bg-red-500'
  return 'bg-surface-400 dark:bg-surface-500'
})
</script>

<template>
  <footer
    class="flex items-center justify-between gap-3 border-t border-surface-200 px-4 py-1.5 text-xs text-surface-400 dark:border-surface-700 dark:text-surface-500"
  >
    <div class="flex items-center gap-3">
      <span>{{ t('app.name') }}</span>
      <span class="hidden sm:inline">{{ t('app.version', { version }) }}</span>
    </div>

    <div class="flex items-center gap-2">
      <span v-if="apiVersion" class="hidden sm:inline">
        {{ t('app.apiVersion', { version: apiVersion }) }}
      </span>
      <span v-if="formattedBuildTime" class="hidden sm:inline">
        {{ t('app.apiBuildTime', { time: formattedBuildTime }) }}
      </span>
      <n-tooltip trigger="hover" placement="top">
        <template #trigger>
          <span
            class="h-2 w-2 shrink-0 rounded-full"
            :class="statusDotClass"
            role="status"
            :aria-label="t(`app.apiStatus.${status}`)"
          />
        </template>
        {{ t(`app.apiStatus.${status}`) }}
      </n-tooltip>
    </div>
  </footer>
</template>
