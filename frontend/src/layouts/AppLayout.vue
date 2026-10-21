<script setup lang="ts">
import { BELOW_LG_QUERY } from '@/lib/responsive'
import AppFooter from './AppFooter.vue'
import AppHeader from './AppHeader.vue'
import AppSidebar from './AppSidebar.vue'
import AppTopMenu from './AppTopMenu.vue'
import AppearanceSettings from './AppearanceSettings.vue'
import CommandPalette from './CommandPalette.vue'
import PageTitle from './PageTitle.vue'

const notificationsStore = useNotificationsStore()
const route = useRoute()
const layout = useLayoutStore()
const belowLg = useMediaQuery(BELOW_LG_QUERY)

// Horizontal on a wide screen: the top bar replaces the header, and the page title moves into the content.
// Below lg it falls back to the header and the mobile drawer like every mode. AppSidebar stays mounted in
// both cases; it owns that drawer and shows no desktop sidebar in horizontal mode.
const topMenu = computed(() => layout.menuMode === 'horizontal' && !belowLg.value)

// Pages scroll inside `main`, not `window`, so the router's `scrollBehavior` never reaches it.
// Keyed on `path` only: a query change (filters, paging) keeps the scroll position.
const mainEl = ref<HTMLElement | null>(null)
watch(
  () => route.path,
  () => mainEl.value?.scrollTo({ top: 0 }),
)

onMounted(() => {
  notificationsStore.startPolling()
  notificationsStore.connectRealtime()
})
onUnmounted(() => {
  notificationsStore.stopPolling()
  notificationsStore.disconnectRealtime()
})
</script>

<template>
  <!--
    `relative` on the shell and `main` is load-bearing: without a positioned ancestor, an absolute
    element (an `sr-only` label) escapes every `overflow` clip and the shell scrolls. `h-dvh`, not
    `h-screen`: mobile `100vh` includes the area under the address bar, hiding the footer.
  -->
  <div
    data-testid="app-shell"
    class="relative flex h-dvh overflow-hidden bg-surface-100 dark:bg-surface-950"
    :class="topMenu && 'flex-col'"
  >
    <AppSidebar />
    <AppTopMenu v-if="topMenu" />
    <!-- `isolate`: a z-index inside the page stays below the drawer's overlay panel (z-20). -->
    <div class="isolate flex min-h-0 flex-1 flex-col overflow-hidden">
      <AppHeader v-if="!topMenu" />
      <main
        ref="mainEl"
        class="relative flex-1 overflow-y-auto p-4 [scrollbar-gutter:stable] lg:p-6"
      >
        <PageTitle v-if="topMenu" class="mb-4" />
        <RouterView />
      </main>
      <AppFooter />
    </div>

    <!--
      Overlays opened from the header (⚙, "Go to…", Ctrl K, the avatar menu); mounted once for the whole
      shell. The profile dialog lives here, not in the avatar menu: horizontal mode swaps the top bar and
      the header at lg, which unmounts that menu and would drop the dialog's unsaved form.
    -->
    <AppearanceSettings />
    <CommandPalette />
    <ProfileDialog v-model:visible="layout.profileOpen" />
  </div>
</template>
