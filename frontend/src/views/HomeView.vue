<script setup lang="ts">
const { t } = useI18n()
</script>

<template>
  <div class="mx-auto container space-y-2">
    <h1 class="text-lg font-semibold text-surface-900 dark:text-surface-0 lg:text-xl">
      {{ t('app.welcomeTitle') }}
    </h1>
    <p class="text-sm text-surface-600 dark:text-surface-300">
      {{ t('app.welcomeDescription') }}
    </p>
  </div>
</template>
