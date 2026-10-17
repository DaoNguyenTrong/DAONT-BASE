<script setup lang="ts">
import { computed } from 'vue'
import { useData } from 'vitepress'
import { localeOf } from '../../paths'
import { strings } from '../strings'

// The app lives at the origin root, outside VitePress's /guide/ base: a plain <a href="/"> leaves
// the guide's router, which a VitePress link (always prefixed with the base) cannot do.
withDefaults(defineProps<{ variant?: 'nav' | 'button' }>(), { variant: 'nav' })
const { page } = useData()
const label = computed(() => strings[localeOf(page.value.relativePath)].openApp)
</script>

<template>
  <a href="/" target="_self" :class="['guide-app-link', `guide-app-link--${variant}`]">{{
    label
  }}</a>
</template>
