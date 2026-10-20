<script setup lang="ts">
import { Pin } from '@vicons/tabler'
import { BELOW_LG_QUERY } from '@/lib/responsive'
import AppSidebarNav from './AppSidebarNav.vue'
import { sidebarThemeClasses } from './sidebar-theme'

const { t } = useI18n()
const router = useRouter()
const layout = useLayoutStore()
const menuEntries = useSidebarMenu()
const { isDark } = useThemePreference()
const { wordmarkSrc } = useBrandWordmark()
const belowLg = useMediaQuery(BELOW_LG_QUERY)

const theme = computed(() => sidebarThemeClasses(layout.menuTheme, isDark.value))

const isTouch = useMediaQuery('(hover: none)')

// The unpinned drawer is a rail until it expands; an expanded one keeps its rail width in the layout and
// lays a full panel over the content (the rail beneath is covered, the page never shifts).
const isDrawer = computed(() => layout.menuMode === 'drawer')
const overlay = computed(() => isDrawer.value && layout.isRail && layout.drawerExpanded)

// `isRail` covers slim, slim+ and the unpinned drawer; static and the pinned drawer show the full sidebar.
// An expanded drawer shows the full menu too.
const railVariant = computed(() =>
  layout.isRail && !overlay.value ? (layout.menuMode === 'slim-plus' ? 'slim-plus' : 'slim') : null,
)
const width = computed(() => (layout.isRail ? layout.railWidth : '16rem'))
const showPin = computed(() => isDrawer.value && !railVariant.value)

// Only the static sidebar can be switched off (the header's ☰); a flag left over from it never hides another mode.
// Horizontal has no sidebar at all (its menu is the top bar), only the mobile drawer below.
const visible = computed(
  () =>
    layout.menuMode !== 'horizontal' && !(layout.menuMode === 'static' && layout.staticInactive),
)

// The wordmark's text color is baked into the image: the white-text file for the purple brand menu and
// for any dark surface, the theme-aware one for the transparent menu in light mode.
const sidebarWordmarkSrc = computed(() =>
  layout.menuTheme === 'brand' || isDark.value
    ? '/weatherplus-wordmark-dark.webp'
    : wordmarkSrc.value,
)

// The mobile drawer turns purple only for the brand menu in light mode; otherwise it keeps the theme's
// default drawer color. Scoped to the drawer via Naive UI's local theme-overrides rather than the global
// Drawer tokens, which every other drawer in the app shares.
const drawerThemeOverrides = computed(() =>
  layout.menuTheme === 'brand' && !isDark.value
    ? {
        Drawer: {
          color: '#6428f4',
          textColor: '#ffffff',
          titleTextColor: '#ffffff',
          closeIconColor: 'rgba(255, 255, 255, 0.7)',
          closeIconColorHover: '#ffffff',
          closeIconColorPressed: 'rgba(255, 255, 255, 0.9)',
          headerBorderBottom: '1px solid rgba(255, 255, 255, 0.12)',
          footerBorderTop: '1px solid rgba(255, 255, 255, 0.12)',
        },
      }
    : undefined,
)

// The drawer exists only below lg; a window widened past it must not leave it open behind the desktop menu.
// An expanded drawer panel is a desktop overlay too, so crossing the breakpoint either way resets it.
watch(belowLg, (below) => {
  if (!below) layout.closeMobile()
  layout.collapseDrawer()
})

// Hover devices open the drawer by entering the rail and close it by leaving the panel. Touch screens
// fire synthetic mouse events on tap, so they are ignored there in favour of the taps below.
function handleRailEnter() {
  if (isDrawer.value && layout.isRail && !isTouch.value) layout.expandDrawer()
}
function handlePanelLeave() {
  if (!isTouch.value) layout.collapseDrawer()
}

// Tabbing into the rail opens the drawer; on touch only keyboard focus counts, since a tap on a
// leaf navigates. The focused entry is swapped for its panel twin, so focus moves to the logo
// button, which stays mounted.
function isKeyboardFocus(target: EventTarget | null) {
  if (!isTouch.value) return true
  try {
    return target instanceof Element && target.matches(':focus-visible')
  } catch {
    return true
  }
}
function handleFocusIn(event: FocusEvent) {
  if (!isDrawer.value || !layout.isRail || belowLg.value || !isKeyboardFocus(event.target)) return
  if (!layout.drawerExpanded) {
    layout.expandDrawer()
    if (event.target !== homeEl.value) homeEl.value?.focus()
  }
}
// Focus leaving the sidebar for another element puts the panel away; focus lost to nowhere does not.
function handleFocusOut(event: FocusEvent) {
  const next = event.relatedTarget
  if (overlay.value && next instanceof Node && !asideEl.value?.contains(next)) {
    layout.collapseDrawer()
  }
}

// A tap on the rail's empty area (the nav itself, not one of its entries) expands the drawer; a tap on an
// entry navigates or opens its flyout as usual.
function handleRailTap(event: MouseEvent) {
  if (
    isDrawer.value &&
    railVariant.value &&
    isTouch.value &&
    event.target === event.currentTarget
  ) {
    layout.expandDrawer()
  }
}

function handleHomeClick() {
  if (isDrawer.value && railVariant.value && isTouch.value) {
    layout.expandDrawer()
    return
  }
  goHome()
}

function goHome() {
  layout.closeMobile()
  router.push(useHomeRoute())
}

// Choosing a page on a touch screen puts the panel away again.
const route = useRoute()
watch(
  () => route.path,
  () => {
    if (isTouch.value) layout.collapseDrawer()
  },
)

const panelEl = ref<HTMLElement | null>(null)
const asideEl = ref<HTMLElement | null>(null)
const homeEl = ref<HTMLElement | null>(null)
function handleDocumentClick(event: MouseEvent) {
  if (overlay.value && panelEl.value && !panelEl.value.contains(event.target as Node)) {
    layout.collapseDrawer()
  }
}
// Esc closes whatever is on top first (an overlay, or a handler that already took the key). The
// overlay state is read in the capture phase: naive-ui closes its drawer/modal from a window
// listener that doesn't prevent the key and may run before this one.
let overlayOnTop = false
function recordOverlay() {
  overlayOnTop = layout.paletteOpen || layout.settingsOpen || layout.profileOpen
}
function handleKeydown(event: KeyboardEvent) {
  if (event.defaultPrevented || overlayOnTop) {
    return
  }
  if (event.key === 'Escape' && layout.drawerExpanded && !layout.drawerPinned) {
    // Focus inside the panel would be dropped to the page when it closes; the logo is on the rail too.
    if (asideEl.value?.contains(document.activeElement)) homeEl.value?.focus()
    layout.collapseDrawer()
  }
}
onMounted(() => {
  document.addEventListener('click', handleDocumentClick)
  window.addEventListener('keydown', recordOverlay, true)
  window.addEventListener('keydown', handleKeydown)
})
onUnmounted(() => {
  document.removeEventListener('click', handleDocumentClick)
  window.removeEventListener('keydown', recordOverlay, true)
  window.removeEventListener('keydown', handleKeydown)
})
</script>

<template>
  <!-- Desktop sidebar -->
  <aside
    v-if="visible"
    data-testid="app-sidebar"
    :data-mode="layout.menuMode"
    :data-theme="layout.menuTheme"
    class="relative hidden shrink-0 flex-col transition-[width] duration-300 ease-in-out lg:flex"
    :class="[theme.surface, layout.menuTheme === 'brand' && ['border-r', theme.divider]]"
    :style="{ width }"
    ref="asideEl"
    @mouseenter="handleRailEnter"
    @focusin="handleFocusIn"
    @focusout="handleFocusOut"
  >
    <!-- The same column serves the rail, the pinned/static sidebar and the drawer's overlay panel. -->
    <div
      ref="panelEl"
      class="flex min-h-0 flex-1 flex-col"
      :class="
        overlay && [
          'absolute inset-y-0 left-0 z-20 w-64 flex-none shadow-xl',
          theme.surface,
          layout.menuTheme === 'brand' && ['border-r', theme.divider],
        ]
      "
      :data-testid="overlay ? 'app-sidebar-panel' : undefined"
      @mouseleave="overlay && handlePanelLeave()"
    >
      <div
        class="flex h-16 shrink-0 items-center"
        :class="layout.menuTheme === 'brand' && ['border-b', theme.divider]"
      >
        <button
          ref="homeEl"
          type="button"
          data-testid="app-sidebar-home"
          class="flex h-full min-w-0 flex-1 cursor-pointer items-center justify-center transition-colors"
          :class="[railVariant ? 'px-2' : 'px-4', theme.itemIdle]"
          @click="handleHomeClick"
        >
          <img
            v-if="railVariant"
            :alt="t('app.name')"
            class="h-9 w-9 shrink-0 object-cover"
            src="/weatherplus-mark.webp"
          />
          <img v-else :alt="t('app.name')" class="h-9 shrink-0" :src="sidebarWordmarkSrc" />
        </button>
        <button
          v-if="showPin"
          type="button"
          data-testid="app-sidebar-pin"
          :aria-pressed="layout.drawerPinned"
          :aria-label="t('layout.pinSidebar')"
          class="mr-3 flex h-9 w-9 shrink-0 pointer-coarse:size-10 cursor-pointer items-center justify-center rounded-lg transition-colors"
          :class="layout.drawerPinned ? theme.itemActive : theme.itemIdle"
          @click="layout.setDrawerPinned(!layout.drawerPinned)"
        >
          <n-icon :component="Pin" class="text-base!" />
        </button>
      </div>

      <AppSidebarNav
        :entries="menuEntries"
        :variant="railVariant ?? 'panel'"
        class="flex-1 overflow-y-auto py-3"
        :class="railVariant ? 'px-2' : 'px-3'"
        @click="handleRailTap"
      />
    </div>
  </aside>

  <!-- Mobile drawer (below lg, every mode) -->
  <n-config-provider abstract :theme-overrides="drawerThemeOverrides">
    <n-drawer v-model:show="layout.mobileOpen" placement="left" :width="280" class="lg:hidden!">
      <n-drawer-content>
        <template #header>
          <button
            type="button"
            class="flex w-full cursor-pointer items-center text-left"
            @click="goHome"
          >
            <img :alt="t('app.name')" class="h-8 w-auto shrink-0" :src="sidebarWordmarkSrc" />
          </button>
        </template>

        <AppSidebarNav
          data-testid="app-sidebar-mobile"
          :entries="menuEntries"
          mobile
          class="px-1 py-2"
        />
      </n-drawer-content>
    </n-drawer>
  </n-config-provider>
</template>
