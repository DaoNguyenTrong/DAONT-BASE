<script setup lang="ts">
import { h } from 'vue'
import type { DropdownOption } from 'naive-ui'
import type { SidebarMenuItem } from './sidebar-menu'

const props = defineProps<{
  item: SidebarMenuItem
  depth: number
  mobile?: boolean
  minimal?: boolean
}>()

const { t } = useI18n()
const router = useRouter()
const route = useRoute()
const sidebar = useSidebarStore()

const expandedKeys = ref<Set<string>>(new Set())

function toggleExpand(key: string) {
  if (expandedKeys.value.has(key)) {
    expandedKeys.value.delete(key)
  } else {
    expandedKeys.value.add(key)
  }
}

function isActive(item: SidebarMenuItem): boolean {
  if (item.routeName) {
    return route.name === item.routeName
  }
  return false
}

const minHeight = computed(() => (props.mobile ? 'min-h-11' : 'min-h-10'))
const iconSize = 'text-base!'

// Minimal mode popup
function toDropdownOptions(items: SidebarMenuItem[]): DropdownOption[] {
  return items.map((item) => ({
    label: t(item.labelKey),
    key: item.routeName ?? item.labelKey,
    icon: () => h(SvgIcon, { name: item.icon }),
    children: item.items ? toDropdownOptions(item.items) : undefined,
  }))
}

const popupOptions = computed(() => (props.item.items ? toDropdownOptions(props.item.items) : []))

function handlePopupSelect(key: string) {
  router.push({ name: key })
}
</script>

<template>
  <!-- Minimal mode: icon only with tooltip -->
  <template v-if="minimal">
    <n-tooltip v-if="!item.items" trigger="hover" placement="right">
      <template #trigger>
        <RouterLink
          :to="{ name: item.routeName }"
          class="mb-1 flex min-h-10 w-full items-center justify-center rounded-lg py-2 transition-colors"
          :class="
            isActive(item)
              ? 'bg-primary-50 text-primary-700 dark:bg-primary-400/10 dark:text-primary-200'
              : 'text-surface-600 hover:bg-surface-100 hover:text-surface-800 dark:text-surface-300 dark:hover:bg-surface-700 dark:hover:text-surface-100'
          "
          @click="sidebar.closeMobile()"
        >
          <SvgIcon :name="item.icon" :class="iconSize" />
        </RouterLink>
      </template>
      {{ t(item.labelKey) }}
    </n-tooltip>
    <n-dropdown
      v-else
      trigger="click"
      placement="right-start"
      :options="popupOptions"
      @select="handlePopupSelect"
    >
      <button
        type="button"
        :aria-label="t(item.labelKey)"
        class="mb-1 flex min-h-10 w-full cursor-pointer items-center justify-center rounded-lg py-2 text-surface-600 transition-colors hover:bg-surface-100 hover:text-surface-800 dark:text-surface-300 dark:hover:bg-surface-700 dark:hover:text-surface-100"
      >
        <SvgIcon :name="item.icon" :class="iconSize" />
      </button>
    </n-dropdown>
  </template>

  <!-- Full / Mobile mode: item with children -->
  <template v-else-if="item.items?.length">
    <button
      type="button"
      class="mb-0.5 flex w-full cursor-pointer items-center gap-3 rounded-lg px-3 py-2 text-left text-surface-600 transition-colors hover:bg-surface-100 hover:text-surface-800 dark:text-surface-300 dark:hover:bg-surface-700 dark:hover:text-surface-100"
      :class="minHeight"
      @click="toggleExpand(item.labelKey)"
    >
      <SvgIcon :name="item.icon" :class="iconSize" />
      <span class="flex-1 truncate text-sm">{{ t(item.labelKey) }}</span>
      <SvgIcon
        :name="expandedKeys.has(item.labelKey) ? 'chevron-down' : 'chevron-right'"
        class="text-xs text-surface-400 transition-transform duration-200"
      />
    </button>
    <div
      v-if="expandedKeys.has(item.labelKey)"
      class="ml-2 border-l border-surface-200 pl-2 dark:border-surface-600"
    >
      <AppSidebarItem
        v-for="child in item.items"
        :key="child.labelKey"
        :item="child"
        :depth="depth + 1"
        :mobile="mobile"
      />
    </div>
  </template>

  <!-- Full / Mobile mode: leaf item -->
  <template v-else>
    <RouterLink
      :to="{ name: item.routeName }"
      class="mb-0.5 flex w-full items-center gap-3 rounded-lg px-3 py-2 text-left transition-colors"
      :class="[
        minHeight,
        isActive(item)
          ? 'bg-primary-50 font-medium text-primary-700 dark:bg-primary-400/10 dark:text-primary-200'
          : 'text-surface-600 hover:bg-surface-100 hover:text-surface-800 dark:text-surface-300 dark:hover:bg-surface-700 dark:hover:text-surface-100',
      ]"
      @click="sidebar.closeMobile()"
    >
      <SvgIcon :name="item.icon" :class="iconSize" />
      <span class="truncate text-sm">{{ t(item.labelKey) }}</span>
    </RouterLink>
  </template>
</template>
