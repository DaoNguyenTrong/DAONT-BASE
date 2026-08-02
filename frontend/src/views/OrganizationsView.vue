<script setup lang="ts">
import { Building, Plus, Users } from '@vicons/tabler'
import type { CreateOrganizationRequest, OrganizationDto } from '@/api/types'
import OrganizationForm from '@/components/OrganizationForm.vue'
import OrganizationMembersDialog from '@/components/OrganizationMembersDialog.vue'
import { organizationRoleLabelKey } from '@/lib/organization-role-label'

const { t } = useI18n()
const { open } = useAppDialogNaive()
const organizationsStore = useOrganizationsStore()
const auth = useAuthStore()

const loading = ref(true)
const membersDialogVisible = ref(false)
const selectedOrganization = ref<OrganizationDto | null>(null)

function openCreateDialog() {
  const state = reactive<CreateOrganizationRequest>({
    name: '',
    slug: '',
  })
  open(OrganizationForm, {
    header: t('organizations.createTitle'),
    data: { state },
    dialogClass: 'w-full! max-w-lg!',
    onConfirm: async (close) => {
      const organization = await organizationsStore.create({
        ...state,
        slug: state.slug.trim().toLowerCase(),
      })
      showSuccessMessage(t('organizations.created'))
      close()
      openMembers(organization)
    },
  })
}

function openMembers(organization: OrganizationDto) {
  selectedOrganization.value = organization
  membersDialogVisible.value = true
}

async function switchToOrganization(organization: OrganizationDto) {
  try {
    await auth.switchOrganization({ organizationId: organization.id })
    showSuccessMessage(t('organizations.switchedTo', { name: organization.name }))
  } catch (error) {
    if (error instanceof ApiError) {
      showErrorMessage(error.problem.title, error.problem.detail ?? t('errors.requestFailed'))
    } else {
      showErrorMessage(t('errors.requestFailed'))
    }
  }
}

onMounted(async () => {
  loading.value = true
  try {
    await organizationsStore.fetchMine()
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="mx-auto container flex h-full flex-col space-y-5">
    <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
      <h1 class="text-lg font-semibold text-surface-800 dark:text-surface-100">
        {{ t('organizations.title') }}
      </h1>
      <NButton type="primary" class="min-h-11" @click="openCreateDialog">
        <template #icon
          ><n-icon><Plus /></n-icon
        ></template>
        {{ t('organizations.createNew') }}
      </NButton>
    </div>

    <div class="min-h-0 flex-1 overflow-y-auto">
      <div
        v-if="!loading && organizationsStore.myOrganizations.length === 0"
        class="flex flex-col items-center justify-center rounded-xl border border-surface-200 bg-surface-0 px-6 py-16 dark:border-surface-800 dark:bg-surface-900"
      >
        <div
          class="mb-4 flex h-14 w-14 items-center justify-center rounded-2xl bg-primary-50 dark:bg-primary-400/10"
        >
          <n-icon class="text-2xl text-primary-500 dark:text-primary-200"><Building /></n-icon>
        </div>
        <p class="text-base font-medium text-surface-700 dark:text-surface-200">
          {{ t('organizations.empty') }}
        </p>
        <p class="mt-1 text-sm text-surface-500 dark:text-surface-400">
          {{ t('organizations.emptyHint') }}
        </p>
      </div>

      <div v-else class="space-y-2">
        <div
          v-for="organization in organizationsStore.myOrganizations"
          :key="organization.id"
          class="flex items-center justify-between gap-3 rounded-xl border border-surface-200 bg-surface-0 p-4 dark:border-surface-800 dark:bg-surface-900"
        >
          <div class="min-w-0 flex-1">
            <div class="flex items-center gap-2">
              <p class="truncate text-sm font-medium text-surface-800 dark:text-surface-100">
                {{ organization.name }}
              </p>
              <NTag :type="organization.status ? 'success' : 'error'" :bordered="false">
                {{ organization.status ? t('organizations.active') : t('organizations.inactive') }}
              </NTag>
              <NTag v-if="auth.organizationId === organization.id" type="info" :bordered="false">
                {{ t('organizations.switchOrganization') }}
              </NTag>
            </div>
            <p class="mt-1 truncate text-xs text-surface-500 dark:text-surface-400">
              {{ organization.slug }} &middot; {{ t('organizations.myRole') }}:
              {{ t(organizationRoleLabelKey(organization.myRole)) }}
            </p>
          </div>
          <div class="flex shrink-0 items-center gap-1">
            <NButton
              v-if="auth.organizationId !== organization.id"
              secondary
              class="min-h-11"
              @click="switchToOrganization(organization)"
            >
              {{ t('organizations.switchOrganization') }}
            </NButton>
            <NButton
              text
              circle
              class="min-h-11 min-w-11"
              :aria-label="t('organizations.members')"
              @click="openMembers(organization)"
            >
              <template #icon
                ><n-icon><Users /></n-icon
              ></template>
            </NButton>
          </div>
        </div>
      </div>
    </div>

    <OrganizationMembersDialog
      v-model:visible="membersDialogVisible"
      :organization="selectedOrganization"
    />
  </div>
</template>
