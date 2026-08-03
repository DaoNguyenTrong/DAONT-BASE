<script setup lang="ts">
import type { FormInst, FormRules } from 'naive-ui'
import type { PermissionDto } from '@/api/types'
import { permissionLabel } from '@/lib/permission-label'

const { t } = useI18n()
const props = defineProps<{
  state: { name: string; permissionCodes: string[] }
  permissionCatalog: PermissionDto[]
}>()
const state = props.state

const formRef = ref<FormInst | null>(null)
const rules = computed<FormRules>(() => ({
  name: [
    { required: true, message: t('organizations.roleNameRequired'), trigger: ['input', 'blur'] },
  ],
}))

function togglePermission(code: string, checked: boolean) {
  const index = state.permissionCodes.indexOf(code)
  if (checked && index === -1) {
    state.permissionCodes.push(code)
  } else if (!checked && index !== -1) {
    state.permissionCodes.splice(index, 1)
  }
}

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
          {{ t('organizations.roleName') }}<RequiredMark />
        </label>
        <n-input
          v-model:value="state.name"
          type="text"
          class="w-full"
          :placeholder="t('organizations.roleNamePlaceholder')"
        />
      </div>
    </n-form-item>
    <div class="space-y-2">
      <label class="text-sm font-medium text-surface-800 dark:text-surface-100">
        {{ t('organizations.rolePermissions') }}
      </label>
      <div class="space-y-2">
        <n-checkbox
          v-for="permission in permissionCatalog"
          :key="permission.code"
          :checked="state.permissionCodes.includes(permission.code)"
          @update:checked="(checked) => togglePermission(permission.code, checked)"
        >
          {{ permissionLabel(permission.code, t) }}
        </n-checkbox>
      </div>
    </div>
  </n-form>
</template>
