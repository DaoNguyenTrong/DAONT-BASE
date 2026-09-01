<script setup lang="ts">
import { Building, Check, ChevronDown, Loader, Settings } from '@vicons/tabler'

const props = defineProps<{
  minimal?: boolean
}>()

const { t } = useI18n()
const router = useRouter()
const auth = useAuthStore()
const organizationsStore = useOrganizationsStore()
const sidebar = useSidebarStore()

const showMenu = ref(false)
const switchingTo = ref<string | null>(null)

const currentLabel = computed(() => auth.organizationName ?? t('organizations.personalWorkspace'))

async function switchTo(organizationId: string | null, name: string | null) {
  if (switchingTo.value !== null || organizationId === auth.organizationId) {
    showMenu.value = false
    return
  }
  switchingTo.value = organizationId ?? 'personal'
  try {
    await auth.switchOrganization({ organizationId })
    showSuccessMessage(
      name ? t('organizations.switchedTo', { name }) : t('organizations.switchedToPersonal'),
    )
  } catch (error) {
    if (error instanceof ApiError) {
      showErrorMessage(error.problem.title, error.problem.detail ?? t('errors.requestFailed'))
    } else {
      showErrorMessage(t('errors.requestFailed'))
    }
  } finally {
    switchingTo.value = null
    showMenu.value = false
  }
}

function manageOrganizations() {
  showMenu.value = false
  sidebar.closeMobile()
  router.push({ name: 'organizations' })
}

watch(showMenu, (open) => {
  if (open && !organizationsStore.loaded) {
    void organizationsStore.fetchMine()
  }
})
</script>

<template>
  <n-popover v-model:show="showMenu" trigger="click" placement="right-start" :width="240">
    <template #trigger>
      <!--
        The trigger slot needs a single, unconditional *element* root. Previously
        this was a v-if/v-else between <n-tooltip> and a bare <button>: the
        popover's VTarget then bound to the tooltip's non-element component root,
        its ref directive never fired, the trigger measured 0×0, and the menu
        detached to the sidebar's top-left corner. Wrapping both modes in one
        <div> gives VTarget a real box to measure and anchor to.
      -->
      <div :class="props.minimal ? 'inline-flex' : 'block'">
        <n-tooltip v-if="props.minimal" trigger="hover" placement="right">
          <template #trigger>
            <button
              type="button"
              :aria-label="`${t('organizations.switchOrganization')}: ${currentLabel}`"
              class="flex h-8 w-8 shrink-0 cursor-pointer items-center justify-center rounded-full bg-white/10 transition-colors hover:bg-white/20 dark:bg-surface-600 dark:hover:bg-primary-400/20"
            >
              <n-icon class="text-sm text-white/80 dark:text-surface-300"><Building /></n-icon>
            </button>
          </template>
          {{ currentLabel }}
        </n-tooltip>
        <button
          v-else
          type="button"
          :aria-label="t('organizations.switchOrganization')"
          class="flex w-full cursor-pointer items-center gap-2 rounded-lg px-2 py-1.5 text-left transition-colors hover:bg-white/10 dark:hover:bg-surface-800"
        >
          <n-icon class="shrink-0 text-sm text-white/80 dark:text-surface-300"><Building /></n-icon>
          <span
            class="min-w-0 flex-1 truncate text-sm font-medium text-white dark:text-surface-200"
          >
            {{ currentLabel }}
          </span>
          <n-icon class="shrink-0 text-sm text-white/60 dark:text-surface-400"
            ><ChevronDown
          /></n-icon>
        </button>
      </div>
    </template>

    <div class="flex max-h-80 min-w-52 flex-col gap-0.5 overflow-y-auto">
      <button
        type="button"
        class="flex cursor-pointer items-center gap-2 rounded-lg px-2 py-1.5 text-left text-sm transition-colors hover:bg-surface-100 dark:hover:bg-surface-800"
        @click="switchTo(null, null)"
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
        class="flex cursor-pointer items-center gap-2 rounded-lg px-2 py-1.5 text-left text-sm transition-colors hover:bg-surface-100 dark:hover:bg-surface-800"
        @click="switchTo(organization.id, organization.name)"
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
        class="flex cursor-pointer items-center gap-2 rounded-lg px-2 py-1.5 text-left text-sm transition-colors hover:bg-surface-100 dark:hover:bg-surface-800"
        @click="manageOrganizations"
      >
        <n-icon class="shrink-0"><Settings /></n-icon>
        <span>{{ t('organizations.manage') }}</span>
      </button>
    </div>
  </n-popover>
</template>
