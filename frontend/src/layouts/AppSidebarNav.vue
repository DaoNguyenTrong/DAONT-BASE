<script setup lang="ts">
import AppSidebarItem from './AppSidebarItem.vue'
import type { SidebarMenuEntry } from './sidebar-menu'
import { sidebarThemeClasses } from './sidebar-theme'

// The menu list shared by the desktop sidebar and the mobile drawer. In the panel a group is a title with
// its items beneath; on a rail a group is a single entry whose items open in a flyout.
withDefaults(
  defineProps<{
    entries: SidebarMenuEntry[]
    variant?: 'panel' | 'slim' | 'slim-plus'
    mobile?: boolean
  }>(),
  { variant: 'panel', mobile: false },
)

const { t } = useI18n()
const layout = useLayoutStore()
const { isDark } = useThemePreference()

const theme = computed(() => sidebarThemeClasses(layout.menuTheme, isDark.value))

function entryKey(entry: SidebarMenuEntry) {
  return entry.kind === 'group' ? `group:${entry.id}` : (entry.routeName ?? entry.labelKey)
}
</script>

<template>
  <nav>
    <template v-for="entry in entries" :key="entryKey(entry)">
      <template v-if="variant === 'panel' && entry.kind === 'group'">
        <p
          class="mt-4 mb-1 px-3 text-xs font-semibold tracking-wider uppercase first:mt-0"
          :class="theme.groupTitle"
        >
          {{ t(entry.labelKey) }}
        </p>
        <AppSidebarItem
          v-for="item in entry.items"
          :key="item.routeName ?? item.labelKey"
          :item="item"
          :depth="0"
          :mobile="mobile"
        />
      </template>
      <AppSidebarItem v-else :item="entry" :depth="0" :variant="variant" :mobile="mobile" />
    </template>
  </nav>
</template>
