<script setup lang="ts">
import { ChevronDown } from '@vicons/tabler'
import AppHeader from './AppHeader.vue'
import { menuDropdownOptions } from './menu-dropdown'
import { splitOverflow } from './overflow-split'
import { containsRoute, type SidebarMenuEntry } from './sidebar-menu'
import { sidebarThemeClasses } from './sidebar-theme'

// The horizontal menu's bar (desktop only): logo · top-level entries · "More" · the header's actions.
const { t } = useI18n()
const router = useRouter()
const route = useRoute()
const layout = useLayoutStore()
const entries = useSidebarMenu()
const { isDark } = useThemePreference()
const { wordmarkSrc } = useBrandWordmark()

const theme = computed(() => sidebarThemeClasses(layout.menuTheme, isDark.value))

// Same rule as the sidebar: the white-text wordmark on the purple bar and on any dark surface.
const barWordmarkSrc = computed(() =>
  layout.menuTheme === 'brand' || isDark.value
    ? '/weatherplus-wordmark-dark.webp'
    : wordmarkSrc.value,
)

// The header's actions are drawn for the page background; the purple bar asks for their white tones. The
// dark brand bar is a dark surface, which their default look already suits.
const headerTone = computed(() =>
  layout.menuTheme === 'brand' && !isDark.value ? 'on-brand' : 'default',
)

const currentRouteName = computed(() => (typeof route.name === 'string' ? route.name : undefined))

function entryKey(entry: SidebarMenuEntry) {
  return entry.kind === 'group' ? `group:${entry.id}` : (entry.routeName ?? entry.labelKey)
}
function holdsCurrent(entry: SidebarMenuEntry) {
  return containsRoute(entry, currentRouteName.value)
}

// Overflow: every entry (and "More") is laid out once more in an invisible copy, which keeps its natural
// width whatever the bar shows; the entries that don't fit beside "More" move into it. A fork adds entries
// and labels change with the locale and font size, so this is measured, never a fixed count.
const entriesEl = ref<HTMLElement | null>(null)
const ghostEl = ref<HTMLElement | null>(null)
const visibleCount = ref<number | null>(null)

const visibleEntries = computed(() => entries.value.slice(0, visibleCount.value ?? undefined))
const overflowEntries = computed(() =>
  visibleCount.value === null ? [] : entries.value.slice(visibleCount.value),
)

function measure() {
  if (!entriesEl.value || !ghostEl.value) return
  const widths = Array.from(
    ghostEl.value.querySelectorAll('[data-measure-item]'),
    (el) => el.getBoundingClientRect().width,
  )
  const moreWidth =
    ghostEl.value.querySelector('[data-measure-more]')?.getBoundingClientRect().width ?? 0
  visibleCount.value = splitOverflow(
    widths,
    entriesEl.value.getBoundingClientRect().width,
    moreWidth,
  )
}

let observer: ResizeObserver | null = null
onMounted(() => {
  // The bar's room changes with the window; the copy's width with the labels.
  observer = new ResizeObserver(() => measure())
  if (entriesEl.value) observer.observe(entriesEl.value)
  if (ghostEl.value) observer.observe(ghostEl.value)
  nextTick(measure)
})
onUnmounted(() => observer?.disconnect())
watch(entries, () => nextTick(measure))

// A group or a parent opens its items downward; "More" holds the overflowed entries, where a group or a
// parent becomes a submenu.
const entryOptions = computed(
  () =>
    new Map(
      entries.value.map((entry) => [entryKey(entry), menuDropdownOptions(entry.items ?? [], t)]),
    ),
)
const moreOptions = computed(() => menuDropdownOptions(overflowEntries.value, t))
const moreHoldsCurrent = computed(() => overflowEntries.value.some(holdsCurrent))

function handleSelect(key: string) {
  router.push({ name: key })
}

function goHome() {
  router.push(useHomeRoute())
}

const entryClass =
  'flex h-10 cursor-pointer items-center gap-2 rounded-lg px-3 text-sm whitespace-nowrap transition-colors'
</script>

<template>
  <div
    data-testid="app-top-menu"
    class="relative flex h-16 shrink-0 items-center gap-3 px-4"
    :class="[theme.surface, layout.menuTheme === 'transparent' && ['border-b', theme.divider]]"
  >
    <button
      type="button"
      data-testid="app-top-menu-home"
      class="flex h-full shrink-0 cursor-pointer items-center pr-2"
      @click="goHome"
    >
      <img :alt="t('app.name')" class="h-9 shrink-0" :src="barWordmarkSrc" />
    </button>

    <!-- `min-w-0` lets the entries shrink below their content, which is what makes them overflow.
         `self-stretch` gives the clip the bar's full height, so focus rings and the active shadow show. -->
    <nav
      ref="entriesEl"
      data-testid="app-top-menu-entries"
      class="flex min-w-0 flex-1 items-center self-stretch overflow-hidden"
    >
      <div v-for="entry in visibleEntries" :key="entryKey(entry)" class="shrink-0 pr-1">
        <n-dropdown
          v-if="entry.items?.length"
          trigger="click"
          placement="bottom-start"
          :options="entryOptions.get(entryKey(entry))"
          :value="currentRouteName"
          @select="handleSelect"
        >
          <button
            type="button"
            aria-haspopup="menu"
            :class="[entryClass, holdsCurrent(entry) ? theme.itemActive : theme.itemIdle]"
          >
            <n-icon :component="entry.icon" class="text-base!" />
            <span>{{ t(entry.labelKey) }}</span>
            <n-icon :component="ChevronDown" class="text-xs opacity-60" />
          </button>
        </n-dropdown>
        <RouterLink
          v-else-if="entry.kind === 'item'"
          :to="{ name: entry.routeName }"
          :class="[entryClass, holdsCurrent(entry) ? theme.itemActive : theme.itemIdle]"
        >
          <n-icon :component="entry.icon" class="text-base!" />
          <span>{{ t(entry.labelKey) }}</span>
        </RouterLink>
      </div>

      <n-dropdown
        v-if="overflowEntries.length"
        trigger="click"
        placement="bottom-start"
        :options="moreOptions"
        :value="currentRouteName"
        @select="handleSelect"
      >
        <button
          type="button"
          aria-haspopup="menu"
          data-testid="app-top-menu-more"
          class="shrink-0"
          :class="[entryClass, moreHoldsCurrent ? theme.itemActive : theme.itemIdle]"
        >
          <span>{{ t('layout.more') }}</span>
          <n-icon :component="ChevronDown" class="text-xs opacity-60" />
        </button>
      </n-dropdown>
    </nav>

    <AppHeader variant="actions" :tone="headerTone" class="shrink-0" />

    <!-- The measuring copy: same boxes as the bar (bold, as when active), never seen or focused. -->
    <div
      ref="ghostEl"
      aria-hidden="true"
      class="pointer-events-none invisible absolute top-0 left-0 flex w-max"
    >
      <span v-for="entry in entries" :key="entryKey(entry)" data-measure-item class="shrink-0 pr-1">
        <span :class="[entryClass, 'font-medium']">
          <n-icon :component="entry.icon" class="text-base!" />
          <span>{{ t(entry.labelKey) }}</span>
          <n-icon v-if="entry.items?.length" :component="ChevronDown" class="text-xs" />
        </span>
      </span>
      <span data-measure-more class="shrink-0">
        <span :class="[entryClass, 'font-medium']">
          <span>{{ t('layout.more') }}</span>
          <n-icon :component="ChevronDown" class="text-xs" />
        </span>
      </span>
    </div>
  </div>
</template>
