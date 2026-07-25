<script setup lang="ts">
const { t } = useI18n()
const hasSocialLogin = Boolean(
  import.meta.env.VITE_GOOGLE_CLIENT_ID || import.meta.env.VITE_MICROSOFT_CLIENT_ID,
)
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
