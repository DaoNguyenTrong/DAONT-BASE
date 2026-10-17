<script setup lang="ts">
import { Check, Loader, Settings } from '@vicons/tabler'

const emit = defineEmits<{ done: [] }>()

const { t } = useI18n()
const router = useRouter()
const auth = useAuthStore()
const organizationsStore = useOrganizationsStore()
const { switchOrganization } = useOrganizationSwitch()

const switchingTo = ref<string | null>(null)

async function switchTo(organizationId: string | null) {
  if (switchingTo.value !== null) {
    return
  }
  if (organizationId === auth.organizationId) {
    emit('done')
    return
  }
  switchingTo.value = organizationId ?? 'personal'
  try {
    // Ends in a full page reload on success — no success toast, it wouldn't survive the reload.
    await switchOrganization(organizationId)
  } catch (error) {
    if (error instanceof ApiError) {
      showErrorMessage(error.problem.title, error.problem.detail ?? t('errors.requestFailed'))
    } else {
      showErrorMessage(t('errors.requestFailed'))
    }
  } finally {
    switchingTo.value = null
    emit('done')
  }
}

function manageOrganizations() {
  emit('done')
  router.push({ name: 'organizations' })
}

onMounted(() => {
  if (!organizationsStore.loaded) {
    void organizationsStore.fetchMine()
  }
})
</script>

<!-- Its rows are menu items: render it inside a `role="menu"` container (the avatar menu). -->
<template>
  <div
    class="flex max-h-80 min-w-52 flex-col gap-0.5 overflow-y-auto"
    data-testid="organization-list"
  >
    <button
      type="button"
      role="menuitemradio"
      :aria-checked="auth.organizationId === null"
      class="flex cursor-pointer items-center gap-2 rounded-lg px-2 py-1.5 text-left text-sm transition-colors hover:bg-surface-100 dark:hover:bg-surface-800"
      @click="switchTo(null)"
    >
      <n-icon class="shrink-0"
        ><Loader v-if="switchingTo === 'personal'" class="animate-spin" /><Check
          v-else-if="auth.organizationId === null"
      /></n-icon>
      <span class="min-w-0 flex-1 truncate">{{ t('organizations.personalWorkspace') }}</span>
    </button>

    <button
      v-for="organization in organizationsStore.myOrganizations"
      :key="organization.id"
      type="button"
      role="menuitemradio"
      :aria-checked="auth.organizationId === organization.id"
      class="flex cursor-pointer items-center gap-2 rounded-lg px-2 py-1.5 text-left text-sm transition-colors hover:bg-surface-100 dark:hover:bg-surface-800"
      @click="switchTo(organization.id)"
    >
      <n-icon class="shrink-0">
        <Loader v-if="switchingTo === organization.id" class="animate-spin" />
        <Check v-else-if="auth.organizationId === organization.id" />
      </n-icon>
      <span class="min-w-0 flex-1 truncate">{{ organization.name }}</span>
    </button>

    <n-divider class="my-1!" />

    <button
      type="button"
      role="menuitem"
      class="flex cursor-pointer items-center gap-2 rounded-lg px-2 py-1.5 text-left text-sm transition-colors hover:bg-surface-100 dark:hover:bg-surface-800"
      data-testid="organization-list-manage"
      @click="manageOrganizations"
    >
      <n-icon class="shrink-0"><Settings /></n-icon>
      <span>{{ t('organizations.manage') }}</span>
    </button>
  </div>
</template>
