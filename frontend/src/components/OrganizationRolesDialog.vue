<script setup lang="ts">
import { Edit, Plus, Trash } from '@vicons/tabler'
import type { OrganizationDto, RoleDto } from '@/api/types'
import { Permissions } from '@/lib/permissions'
import { permissionLabel } from '@/lib/permission-label'
import RoleForm from '@/components/RoleForm.vue'

const props = defineProps<{
  organization: OrganizationDto | null
}>()
const visible = defineModel<boolean>('visible', { required: true })

const { t } = useI18n()
const { open } = useAppDialogNaive()
const rolesStore = useRolesStore()

const loading = ref(true)
const loadError = ref<string | null>(null)
const deletingId = ref<string | null>(null)

const canManage = computed(
  () =>
    props.organization?.myPermissionCodes.includes(Permissions.OrganizationRolesManage) ?? false,
)

function reportError(error: unknown) {
  if (error instanceof ApiError) {
    showErrorMessage(error.problem.title, error.problem.detail ?? t('errors.requestFailed'))
  } else {
    showErrorMessage(t('errors.requestFailed'))
  }
}

async function loadRoles() {
  if (!props.organization) return
  loading.value = true
  loadError.value = null
  try {
    await Promise.all([
      rolesStore.fetchRoles(props.organization.id),
      rolesStore.fetchPermissionCatalog(),
    ])
  } catch (error) {
    loadError.value =
      error instanceof ApiError
        ? (error.problem.detail ?? error.problem.title)
        : t('errors.requestFailed')
  } finally {
    loading.value = false
  }
}

function openCreateDialog() {
  if (!props.organization) return
  const organizationId = props.organization.id
  const state = reactive({ name: '', permissionCodes: [] as string[] })
  open(RoleForm, {
    header: t('organizations.createRoleTitle'),
    data: { state, permissionCatalog: rolesStore.permissionCatalog },
    dialogClass: 'w-full! max-w-lg!',
    onConfirm: async (close) => {
      await rolesStore.create(organizationId, { ...state })
      showSuccessMessage(t('organizations.roleCreated'))
      close()
    },
  })
}

function openEditDialog(role: RoleDto) {
  if (!props.organization) return
  const organizationId = props.organization.id
  const state = reactive({ name: role.name, permissionCodes: [...role.permissionCodes] })
  open(RoleForm, {
    header: t('organizations.editRoleTitle'),
    data: { state, permissionCatalog: rolesStore.permissionCatalog },
    dialogClass: 'w-full! max-w-lg!',
    onConfirm: async (close) => {
      await rolesStore.update(organizationId, role.id, { ...state })
      showSuccessMessage(t('organizations.roleUpdated'))
      close()
    },
  })
}

function confirmDelete(role: RoleDto) {
  requestConfirmation({
    header: t('common.confirm'),
    message: t('organizations.roleDeleteConfirm'),
    rejectLabel: t('common.cancel'),
    acceptLabel: t('common.confirm'),
    accept: async () => {
      if (!props.organization) return
      deletingId.value = role.id
      try {
        await rolesStore.remove(props.organization.id, role.id)
        showSuccessMessage(t('organizations.roleDeleted'))
      } catch (error) {
        reportError(error)
      } finally {
        deletingId.value = null
      }
    },
  })
}

watch(visible, (open) => {
  if (open) {
    void loadRoles()
  }
})
</script>

<template>
  <n-modal
    v-model:show="visible"
    preset="card"
    :title="
      organization
        ? t('organizations.rolesTitle', { name: organization.name })
        : t('organizations.roles')
    "
    :aria-label="t('organizations.roles')"
    class="w-full max-w-2xl"
  >
    <div v-if="loading" class="space-y-2 pt-2">
      <n-skeleton v-for="i in 3" :key="i" height="4rem" style="border-radius: 0.75rem" />
    </div>

    <n-alert v-else-if="loadError" type="error" :show-icon="false" class="mt-2">
      {{ loadError }}
    </n-alert>

    <template v-else>
      <ul v-if="rolesStore.roles.length > 0" class="mt-1 space-y-2">
        <li
          v-for="role in rolesStore.roles"
          :key="role.id"
          class="flex items-center justify-between gap-3 rounded-xl border border-surface-200 p-3 dark:border-surface-800"
        >
          <div class="min-w-0 flex-1">
            <div class="flex items-center gap-2">
              <p class="truncate text-sm font-medium text-surface-800 dark:text-surface-100">
                {{ role.name }}
              </p>
              <n-tag v-if="role.isSystem" size="small" :bordered="false">
                {{ t('organizations.roleSystemBadge') }}
              </n-tag>
            </div>
            <div class="mt-1 flex flex-wrap gap-1">
              <n-tag
                v-for="code in role.permissionCodes"
                :key="code"
                size="small"
                type="info"
                :bordered="false"
              >
                {{ permissionLabel(code, t) }}
              </n-tag>
              <span
                v-if="role.permissionCodes.length === 0"
                class="text-xs text-surface-500 dark:text-surface-400"
              >
                {{ t('organizations.roleNoPermissions') }}
              </span>
            </div>
          </div>
          <div v-if="canManage && !role.isSystem" class="flex shrink-0 items-center gap-1">
            <n-button
              text
              circle
              class="min-h-11 min-w-11"
              :aria-label="t('organizations.editRoleTitle')"
              @click="openEditDialog(role)"
            >
              <template #icon
                ><n-icon><Edit /></n-icon
              ></template>
            </n-button>
            <n-button
              text
              circle
              type="error"
              class="min-h-11 min-w-11"
              :aria-label="t('organizations.roleDeleteConfirm')"
              :loading="deletingId === role.id"
              @click="confirmDelete(role)"
            >
              <template #icon
                ><n-icon><Trash /></n-icon
              ></template>
            </n-button>
          </div>
        </li>
      </ul>
      <p v-else class="mt-2 text-sm text-surface-500 dark:text-surface-400">
        {{ t('organizations.rolesEmpty') }}
      </p>

      <n-button
        v-if="canManage"
        type="primary"
        class="mt-4 min-h-11 w-full"
        @click="openCreateDialog"
      >
        <template #icon
          ><n-icon><Plus /></n-icon
        ></template>
        {{ t('organizations.createRole') }}
      </n-button>
    </template>
  </n-modal>
</template>
