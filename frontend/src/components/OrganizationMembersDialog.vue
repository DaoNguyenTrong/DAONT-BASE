<script setup lang="ts">
import { Plus, Trash } from '@vicons/tabler'
import { getOrganizations } from '@/api/generated/organizations/organizations'
import type { AddMemberRequest, OrganizationDto, OrganizationMemberDto } from '@/api/types'
import { Permissions } from '@/lib/permissions'

const props = defineProps<{
  organization: OrganizationDto | null
}>()
const visible = defineModel<boolean>('visible', { required: true })

const { t } = useI18n()
const client = getOrganizations()
const organizationsStore = useOrganizationsStore()
const rolesStore = useRolesStore()

const members = ref<OrganizationMemberDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
const adding = ref(false)
const removingId = ref<string | null>(null)
const updatingRolesId = ref<string | null>(null)
const deactivating = ref(false)

const addForm = reactive<AddMemberRequest>({
  email: '',
  roleIds: [],
})

const roleOptions = computed(() =>
  rolesStore.roles.map((role) => ({ label: role.name, value: role.id })),
)

const isOwner = computed(
  () => props.organization?.myPermissionCodes.includes(Permissions.OrganizationManage) ?? false,
)
const canManage = computed(
  () =>
    props.organization?.myPermissionCodes.includes(Permissions.OrganizationMembersManage) ?? false,
)

function reportError(error: unknown) {
  if (error instanceof ApiError) {
    showErrorMessage(error.problem.title, error.problem.detail ?? t('errors.requestFailed'))
  } else {
    showErrorMessage(t('errors.requestFailed'))
  }
}

async function loadMembers() {
  if (!props.organization) return
  loading.value = true
  loadError.value = null
  try {
    ;[members.value] = await Promise.all([
      client.organizationsGetMembers(props.organization.id),
      rolesStore.fetchRoles(props.organization.id),
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

async function addMember() {
  if (!props.organization || adding.value) return
  adding.value = true
  try {
    await client.organizationsAddMember(props.organization.id, {
      ...addForm,
      email: addForm.email.trim(),
    })
    showSuccessMessage(t('organizations.memberAdded'))
    addForm.email = ''
    addForm.roleIds = []
    await loadMembers()
  } catch (error) {
    reportError(error)
  } finally {
    adding.value = false
  }
}

async function changeRoles(member: OrganizationMemberDto, roleIds: string[]) {
  if (!props.organization || roleIds.length === 0) return
  updatingRolesId.value = member.accountId
  try {
    await client.organizationsUpdateMemberRoles(props.organization.id, member.accountId, {
      roleIds,
    })
    member.roleIds = roleIds
    member.roleNames = rolesStore.roles
      .filter((role) => roleIds.includes(role.id))
      .map((role) => role.name)
    showSuccessMessage(t('organizations.memberRolesUpdated'))
  } catch (error) {
    reportError(error)
  } finally {
    updatingRolesId.value = null
  }
}

function confirmRemoveMember(member: OrganizationMemberDto) {
  requestConfirmation({
    header: t('common.confirm'),
    message: t('organizations.removeMemberConfirm'),
    rejectLabel: t('common.cancel'),
    acceptLabel: t('common.confirm'),
    accept: async () => {
      if (!props.organization) return
      removingId.value = member.accountId
      try {
        await client.organizationsRemoveMember(props.organization.id, member.accountId)
        members.value = members.value.filter((m) => m.accountId !== member.accountId)
        showSuccessMessage(t('organizations.memberRemoved'))
      } catch (error) {
        reportError(error)
      } finally {
        removingId.value = null
      }
    },
  })
}

function confirmDeactivate() {
  requestConfirmation({
    header: t('common.confirm'),
    message: t('organizations.deactivateConfirm'),
    rejectLabel: t('common.cancel'),
    acceptLabel: t('common.confirm'),
    accept: async () => {
      if (!props.organization) return
      deactivating.value = true
      try {
        await organizationsStore.deactivate(props.organization.id)
        showSuccessMessage(t('organizations.deactivated'))
        visible.value = false
      } catch (error) {
        reportError(error)
      } finally {
        deactivating.value = false
      }
    },
  })
}

watch(visible, (open) => {
  if (open) {
    void loadMembers()
  }
})
</script>

<template>
  <n-modal
    v-model:show="visible"
    preset="card"
    :title="
      organization
        ? t('organizations.membersTitle', { name: organization.name })
        : t('organizations.members')
    "
    :aria-label="t('organizations.members')"
    class="w-full max-w-2xl"
  >
    <div v-if="loading" class="space-y-2 pt-2">
      <n-skeleton v-for="i in 2" :key="i" height="4rem" style="border-radius: 0.75rem" />
    </div>

    <n-alert v-else-if="loadError" type="error" :show-icon="false" class="mt-2">
      {{ loadError }}
    </n-alert>

    <template v-else>
      <ul v-if="members.length > 0" class="mt-1 space-y-2">
        <li
          v-for="member in members"
          :key="member.accountId"
          class="flex items-center justify-between gap-3 rounded-xl border border-surface-200 p-3 dark:border-surface-800"
        >
          <div class="min-w-0 flex-1">
            <p class="truncate text-sm font-medium text-surface-800 dark:text-surface-100">
              {{ member.accountName }}
            </p>
            <p class="truncate text-xs text-surface-500 dark:text-surface-400">
              {{ member.email }}
            </p>
          </div>
          <n-select
            :value="member.roleIds"
            :options="roleOptions"
            multiple
            :disabled="!canManage"
            :loading="updatingRolesId === member.accountId"
            class="w-48! shrink-0"
            @update:value="(roleIds) => changeRoles(member, roleIds)"
          />
          <n-button
            v-if="canManage"
            text
            circle
            type="error"
            class="min-h-11 min-w-11 shrink-0"
            :aria-label="t('organizations.removeMember')"
            :loading="removingId === member.accountId"
            @click="confirmRemoveMember(member)"
          >
            <template #icon
              ><n-icon><Trash /></n-icon
            ></template>
          </n-button>
        </li>
      </ul>
      <p v-else class="mt-2 text-sm text-surface-500 dark:text-surface-400">
        {{ t('organizations.membersEmpty') }}
      </p>

      <form
        v-if="canManage"
        class="mt-4 flex flex-col gap-2 sm:flex-row"
        @submit.prevent="addMember"
      >
        <n-input
          v-model:value="addForm.email"
          type="text"
          class="w-full flex-1"
          :placeholder="t('organizations.memberEmailPlaceholder')"
          :input-props="{ type: 'email' }"
        />
        <n-select
          v-model:value="addForm.roleIds"
          :options="roleOptions"
          multiple
          class="w-full! sm:w-48!"
        />
        <n-button type="primary" attr-type="submit" :loading="adding" class="min-h-11 shrink-0">
          <template #icon
            ><n-icon><Plus /></n-icon
          ></template>
          {{ t('organizations.addMember') }}
        </n-button>
      </form>

      <div v-if="isOwner" class="mt-6 border-t border-surface-200 pt-4 dark:border-surface-800">
        <n-button
          type="error"
          ghost
          class="min-h-11 w-full"
          :loading="deactivating"
          @click="confirmDeactivate"
        >
          {{ t('organizations.deactivate') }}
        </n-button>
      </div>
    </template>
  </n-modal>
</template>
