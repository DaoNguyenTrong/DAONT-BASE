<script setup lang="ts">
defineProps<{
  name: string
  size?: string | number
  color?: string
}>()
</script>

<template>
  <svg
    aria-hidden="true"
    class="align-middle"
    :style="{ width: size ? `${size}px` : '1em', height: size ? `${size}px` : '1em', color }"
  >
    <use :xlink:href="`#icon-${name}`" fill="currentColor" />
  </svg>
</template>
