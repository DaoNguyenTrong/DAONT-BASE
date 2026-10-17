<script setup lang="ts">
import { Building, ChevronDown } from '@vicons/tabler'
import { BELOW_LG_QUERY } from '@/lib/responsive'

const props = defineProps<{
  minimal?: boolean
}>()

const { t } = useI18n()
const auth = useAuthStore()
const sidebar = useSidebarStore()

const showMenu = ref(false)

// Below `lg` the switcher only sits at the foot of the 280px mobile drawer, where a 240px menu opening to
// the right runs off the screen — open it upward there; beside the desktop sidebar keep `right-start`.
const belowLg = useMediaQuery(BELOW_LG_QUERY)
const menuPlacement = computed(() => (belowLg.value ? 'top-start' : 'right-end'))

const currentLabel = computed(() => auth.organizationName ?? t('organizations.personalWorkspace'))

function onDone() {
  showMenu.value = false
  sidebar.closeMobile()
}
</script>

<template>
  <n-popover v-model:show="showMenu" trigger="click" :placement="menuPlacement" :width="240">
    <template #trigger>
      <!--
        The trigger slot needs a single, unconditional *element* root. Previously
        this was a v-if/v-else between <n-tooltip> and a bare <button>: the
        popover's VTarget then bound to the tooltip's non-element component root,
        its ref directive never fired, the trigger measured 0×0, and the menu
        detached to the sidebar's top-left corner. Wrapping both modes in one
        <div> gives VTarget a real box to measure and anchor to.
      -->
      <div :class="props.minimal ? 'inline-flex' : 'block'" data-testid="organization-switcher">
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

    <OrganizationList @done="onDone" />
  </n-popover>
</template>
