<script setup lang="ts">
import type { CreateAccountRequest } from '@/api/types'

const { t } = useI18n()
const props = defineProps<{
  state: CreateAccountRequest
  isEditing: boolean
}>()
const state = props.state
const isEditing = props.isEditing
</script>

<template>
  <div class="grid gap-4 pt-2 md:grid-cols-2">
    <div class="space-y-2">
      <label class="text-sm font-medium text-surface-800 dark:text-surface-100">
        {{ t('accounts.name') }}<RequiredMark />
      </label>
      <n-input
        v-model:value="state.name"
        type="text"
        class="w-full"
        :placeholder="t('accounts.namePlaceholder')"
      />
    </div>
    <div class="space-y-2">
      <label class="text-sm font-medium text-surface-800 dark:text-surface-100">
        {{ t('accounts.username') }}<RequiredMark />
      </label>
      <n-input
        v-model:value="state.username"
        type="text"
        class="w-full"
        :placeholder="t('accounts.usernamePlaceholder')"
      />
    </div>
    <div class="space-y-2">
      <label class="text-sm font-medium text-surface-800 dark:text-surface-100">
        {{ t('accounts.email') }}<RequiredMark />
      </label>
      <n-input
        v-model:value="state.email"
        type="text"
        class="w-full"
        :placeholder="t('accounts.emailPlaceholder')"
      />
    </div>
    <div v-if="!isEditing" class="space-y-2">
      <label class="text-sm font-medium text-surface-800 dark:text-surface-100">
        {{ t('accounts.password') }}<RequiredMark />
      </label>
      <n-input
        v-model:value="state.password"
        type="password"
        show-password-on="click"
        class="w-full"
        :placeholder="t('accounts.passwordPlaceholder')"
      />
    </div>
    <div class="space-y-2">
      <label class="text-sm font-medium text-surface-800 dark:text-surface-100">
        {{ t('accounts.phone') }}
      </label>
      <n-input
        v-model:value="state.phone"
        type="text"
        class="w-full"
        :placeholder="t('accounts.phonePlaceholder')"
      />
    </div>
    <div class="space-y-2">
      <label class="text-sm font-medium text-surface-800 dark:text-surface-100">
        {{ t('accounts.position') }}
      </label>
      <n-input
        v-model:value="state.position"
        type="text"
        class="w-full"
        :placeholder="t('accounts.positionPlaceholder')"
      />
    </div>
    <div class="space-y-2">
      <label class="text-sm font-medium text-surface-800 dark:text-surface-100">
        {{ t('accounts.address') }}
      </label>
      <n-input
        v-model:value="state.address"
        type="text"
        class="w-full"
        :placeholder="t('accounts.addressPlaceholder')"
      />
    </div>
    <div
      class="flex items-center justify-between rounded-2xl border border-surface-200 px-4 py-3 dark:border-surface-800"
    >
      <label class="text-sm font-medium text-surface-800 dark:text-surface-100">
        {{ t('accounts.status') }}
      </label>
      <n-checkbox v-model:checked="state.status" />
    </div>
  </div>
</template>
