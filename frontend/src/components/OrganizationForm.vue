<script setup lang="ts">
import type { FormInst, FormRules } from 'naive-ui'
import type { CreateOrganizationRequest } from '@/api/types'

const { t } = useI18n()
const props = defineProps<{
  state: CreateOrganizationRequest
}>()
const state = props.state

const formRef = ref<FormInst | null>(null)
const rules = computed<FormRules>(() => ({
  name: [{ required: true, message: t('organizations.nameRequired'), trigger: ['input', 'blur'] }],
  slug: [{ required: true, message: t('organizations.slugRequired'), trigger: ['input', 'blur'] }],
}))

// Called by useAppDialogNaive's open() before onConfirm — see
// src/composables/use-app-dialog-naive.ts.
async function validate() {
  await formRef.value?.validate()
}
defineExpose({ validate })
</script>

<template>
  <n-form ref="formRef" :model="state" :rules="rules" class="grid gap-4 pt-2">
    <n-form-item path="name" :show-label="false" first>
      <div class="w-full space-y-2">
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
    </n-form-item>
    <n-form-item path="slug" :show-label="false" first>
      <div class="w-full space-y-2">
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
    </n-form-item>
  </n-form>
</template>
