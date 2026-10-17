<script setup lang="ts">
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

// `isRail` covers slim, slim+ and the unpinned drawer (the drawer's hover panel comes later); every other
// mode, horizontal included until its top bar exists, shows the full sidebar.
const railVariant = computed(() =>
  layout.isRail ? (layout.menuMode === 'slim-plus' ? 'slim-plus' : 'slim') : null,
)
const width = computed(() => (layout.isRail ? layout.railWidth : '16rem'))

// Only the static sidebar can be switched off (the header's ☰); a flag left over from it never hides another mode.
const visible = computed(() => !(layout.menuMode === 'static' && layout.staticInactive))

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
          color: '#5e4c9e',
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
watch(belowLg, (below) => {
  if (!below) layout.closeMobile()
})

function goHome() {
  layout.closeMobile()
  router.push(useHomeRoute())
}
</script>

<template>
  <!-- Desktop sidebar -->
  <aside
    v-if="visible"
    data-testid="app-sidebar"
    :data-mode="layout.menuMode"
    :data-theme="layout.menuTheme"
    class="hidden shrink-0 flex-col transition-[width] duration-300 ease-in-out lg:flex"
    :class="[theme.surface, layout.menuTheme === 'brand' && ['border-r', theme.divider]]"
    :style="{ width }"
  >
    <button
      type="button"
      data-testid="app-sidebar-home"
      class="flex h-16 w-full shrink-0 cursor-pointer items-center justify-center transition-colors"
      :class="[
        railVariant ? 'px-2' : 'px-4',
        theme.itemIdle,
        layout.menuTheme === 'brand' && ['border-b', theme.divider],
      ]"
      @click="goHome"
    >
      <img
        v-if="railVariant"
        :alt="t('app.name')"
        class="h-9 w-9 shrink-0 object-cover"
        src="/weatherplus-mark.webp"
      />
      <img v-else :alt="t('app.name')" class="h-9 shrink-0" :src="sidebarWordmarkSrc" />
    </button>

    <AppSidebarNav
      :entries="menuEntries"
      :variant="railVariant ?? 'panel'"
      class="flex-1 overflow-y-auto py-3"
      :class="railVariant ? 'px-2' : 'px-3'"
    />
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
