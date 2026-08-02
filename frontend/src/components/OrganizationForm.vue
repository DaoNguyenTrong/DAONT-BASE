<script setup lang="ts">
import type { CreateOrganizationRequest } from '@/api/types'

const { t } = useI18n()
const props = defineProps<{
  state: CreateOrganizationRequest
}>()
const state = props.state
</script>

<template>
  <div class="grid gap-4 pt-2">
    <div class="space-y-2">
      <label class="text-sm font-medium text-surface-800 dark:text-surface-100">
        {{ t('organizations.name') }}<RequiredMark />
      </label>
      <n-input
        v-model:value="state.name"
        type="text"
        class="w-full"
        :placeholder="t('organizations.namePlaceholder')"
      />
    </div>
    <div class="space-y-2">
      <label class="text-sm font-medium text-surface-800 dark:text-surface-100">
        {{ t('organizations.slug') }}<RequiredMark />
      </label>
      <n-input
        v-model:value="state.slug"
        type="text"
        class="w-full"
        :placeholder="t('organizations.slugPlaceholder')"
      />
      <p class="text-xs text-surface-500 dark:text-surface-400">
        {{ t('organizations.slugHint') }}
      </p>
    </div>
  </div>
</template>
