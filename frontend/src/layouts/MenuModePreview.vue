<script setup lang="ts">
import type { MenuMode } from '@/lib/layout-preferences'

defineProps<{ mode: MenuMode }>()
</script>

<template>
  <!-- Decorative thumbnail: a header bar plus the menu placement for the mode. -->
  <div
    aria-hidden="true"
    class="border-surface-300 bg-surface-50 dark:border-surface-600 dark:bg-surface-900 relative h-10 w-full overflow-hidden rounded border"
  >
    <div class="bg-surface-300 dark:bg-surface-600 absolute inset-x-0 top-0 h-2" />
    <div
      v-if="mode === 'horizontal'"
      class="bg-primary-600 absolute inset-x-0 top-2 h-1.5"
      data-part="menu"
    />
    <div
      v-else-if="mode === 'drawer'"
      class="bg-primary-600 absolute top-2 bottom-0 left-0 w-1 opacity-60"
      data-part="menu"
    />
    <div
      v-else
      class="bg-primary-600 absolute top-2 bottom-0 left-0"
      :class="{ 'w-5': mode === 'static', 'w-2': mode === 'slim', 'w-3': mode === 'slim-plus' }"
      data-part="menu"
    />
  </div>
</template>
