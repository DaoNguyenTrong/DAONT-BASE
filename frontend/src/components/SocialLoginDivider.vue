<script setup lang="ts">
const { t } = useI18n()
const runtimeConfig = getRuntimeConfig()
const hasSocialLogin = Boolean(runtimeConfig.googleClientId || runtimeConfig.microsoftClientId)
</script>

<template>
  <div v-if="hasSocialLogin" class="flex items-center gap-3">
    <div class="h-px flex-1 bg-surface-200 dark:bg-surface-800" />
    <span class="text-xs text-surface-500 dark:text-surface-400">{{
      t('auth.orContinueWith')
    }}</span>
    <div class="h-px flex-1 bg-surface-200 dark:bg-surface-800" />
  </div>
</template>
