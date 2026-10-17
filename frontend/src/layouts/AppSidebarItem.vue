<script setup lang="ts">
import type { DropdownGroupOption } from 'naive-ui'
import { ChevronDown, ChevronRight } from '@vicons/tabler'
import { menuDropdownOptions } from './menu-dropdown'
import { containsRoute, type SidebarMenuItem } from './sidebar-menu'
import { sidebarThemeClasses } from './sidebar-theme'

// 'panel' is the full-width list (static sidebar, mobile drawer): children open as an accordion.
// 'slim' / 'slim-plus' are rail entries: children open in a flyout to the right.
const props = withDefaults(
  defineProps<{
    item: SidebarMenuItem
    depth: number
    variant?: 'panel' | 'slim' | 'slim-plus'
    mobile?: boolean
  }>(),
  { variant: 'panel', mobile: false },
)

const { t } = useI18n()
const router = useRouter()
const route = useRoute()
const layout = useLayoutStore()
const { isDark } = useThemePreference()

const theme = computed(() => sidebarThemeClasses(layout.menuTheme, isDark.value))
const label = computed(() => t(props.item.labelKey))
const hasChildren = computed(() => Boolean(props.item.items?.length))
const currentRouteName = computed(() => (typeof route.name === 'string' ? route.name : undefined))
const isCurrent = computed(
  () => Boolean(props.item.routeName) && props.item.routeName === currentRouteName.value,
)
const holdsCurrent = computed(() => containsRoute(props.item, currentRouteName.value))

// Accordion: open on arrival when it holds the current page, and again whenever navigation moves into it.
const expanded = ref(holdsCurrent.value)
watch(holdsCurrent, (holds) => {
  if (holds) expanded.value = true
})

const minHeight = computed(() => (props.mobile ? 'min-h-11' : 'min-h-10'))
const iconSize = 'text-base!'
const railClass = computed(() =>
  props.variant === 'slim-plus'
    ? 'mb-1 flex w-full flex-col items-center gap-1 rounded-lg px-1 py-2 transition-colors'
    : 'mb-1 flex min-h-10 w-full items-center justify-center rounded-lg py-2 transition-colors',
)

// The flyout leads with the entry's own name as a header row.
const flyoutOptions = computed<DropdownGroupOption[]>(() => [
  {
    type: 'group',
    key: `group:${props.item.labelKey}`,
    label: label.value,
    children: menuDropdownOptions(props.item.items ?? [], t),
  },
])

function handleFlyoutSelect(key: string) {
  router.push({ name: key })
}
</script>

<template>
  <!-- Rail: one entry per top-level item or group -->
  <template v-if="variant !== 'panel'">
    <n-dropdown
      v-if="hasChildren"
      trigger="click"
      placement="right-start"
      :options="flyoutOptions"
      :value="currentRouteName"
      @select="handleFlyoutSelect"
    >
      <button
        type="button"
        aria-haspopup="menu"
        :aria-label="label"
        :title="variant === 'slim-plus' ? label : undefined"
        class="cursor-pointer"
        :class="[railClass, holdsCurrent ? theme.itemActive : theme.itemIdle]"
      >
        <n-icon :component="item.icon" :class="iconSize" />
        <span
          v-if="variant === 'slim-plus'"
          class="line-clamp-2 w-full text-center text-[0.6875rem] leading-tight break-words"
          >{{ label }}</span
        >
      </button>
    </n-dropdown>

    <RouterLink
      v-else-if="variant === 'slim-plus'"
      :to="{ name: item.routeName }"
      :title="label"
      :class="[railClass, isCurrent ? theme.itemActive : theme.itemIdle]"
    >
      <n-icon :component="item.icon" :class="iconSize" />
      <span class="line-clamp-2 w-full text-center text-[0.6875rem] leading-tight break-words">{{
        label
      }}</span>
    </RouterLink>

    <n-tooltip v-else trigger="hover" placement="right">
      <template #trigger>
        <RouterLink
          :to="{ name: item.routeName }"
          :aria-label="label"
          :class="[railClass, isCurrent ? theme.itemActive : theme.itemIdle]"
        >
          <n-icon :component="item.icon" :class="iconSize" />
        </RouterLink>
      </template>
      {{ label }}
    </n-tooltip>
  </template>

  <!-- Panel: item with children -->
  <template v-else-if="hasChildren">
    <button
      type="button"
      :aria-expanded="expanded"
      class="mb-0.5 flex w-full cursor-pointer items-center gap-3 rounded-lg px-3 py-2 text-left transition-colors"
      :class="[minHeight, holdsCurrent ? [theme.text, 'font-semibold'] : theme.itemIdle]"
      @click="expanded = !expanded"
    >
      <n-icon :component="item.icon" :class="iconSize" />
      <span class="flex-1 truncate text-sm">{{ label }}</span>
      <n-icon class="text-xs opacity-60">
        <component :is="expanded ? ChevronDown : ChevronRight" />
      </n-icon>
    </button>
    <div v-if="expanded" class="ml-2 border-l pl-2" :class="theme.divider">
      <AppSidebarItem
        v-for="child in item.items"
        :key="child.routeName ?? child.labelKey"
        :item="child"
        :depth="depth + 1"
        :mobile="mobile"
      />
    </div>
  </template>

  <!-- Panel: leaf item -->
  <RouterLink
    v-else
    :to="{ name: item.routeName }"
    class="mb-0.5 flex w-full items-center gap-3 rounded-lg px-3 py-2 text-left transition-colors"
    :class="[minHeight, isCurrent ? theme.itemActive : theme.itemIdle]"
    @click="layout.closeMobile()"
  >
    <n-icon :component="item.icon" :class="iconSize" />
    <span class="truncate text-sm">{{ label }}</span>
  </RouterLink>
</template>
