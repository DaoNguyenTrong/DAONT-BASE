<script setup lang="ts">
import type { PermissionDto } from '@/api/types'
import { permissionLabel } from '@/lib/permission-label'

const { t } = useI18n()
const props = defineProps<{
  state: { name: string; permissionCodes: string[] }
  permissionCatalog: PermissionDto[]
}>()
const state = props.state

function togglePermission(code: string, checked: boolean) {
  const index = state.permissionCodes.indexOf(code)
  if (checked && index === -1) {
    state.permissionCodes.push(code)
  } else if (!checked && index !== -1) {
    state.permissionCodes.splice(index, 1)
  }
}
</script>

<template>
  <div class="grid gap-4 pt-2">
    <div class="space-y-2">
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
  </div>
</template>
