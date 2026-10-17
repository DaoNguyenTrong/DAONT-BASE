<script setup lang="ts">
import { computed } from 'vue'
import { useData } from 'vitepress'
import { localeOf } from '../../paths'
import { permissionLabels } from '../permission-labels'
import { strings } from '../strings'

const { frontmatter, page } = useData()
const locale = computed(() => localeOf(page.value.relativePath))
const code = computed(() => frontmatter.value.permission as string | undefined)
const label = computed(() =>
  code.value ? (permissionLabels[code.value]?.[locale.value] ?? code.value) : '',
)
</script>

<template>
  <div v-if="code" class="guide-permission">
    {{ strings[locale].requiresPermission }}: <strong>{{ label }}</strong>
  </div>
</template>
