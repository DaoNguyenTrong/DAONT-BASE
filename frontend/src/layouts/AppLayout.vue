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
    `relative` on the shell and on `main` is load-bearing: an absolutely positioned element with no
    positioned ancestor (an `sr-only` label, a hidden table caption) takes the page as its containing
    block, escapes every `overflow` clip and stretches the document — the whole shell then scrolls up
    and exposes a blank strip. `h-dvh`, not `h-screen`: on mobile `100vh` includes the area under the
    browser's address bar, hiding the footer.
  -->
  <div
    data-testid="app-shell"
    class="relative flex h-dvh overflow-hidden bg-surface-100 dark:bg-surface-950"
    :class="topMenu && 'flex-col'"
  >
    <AppSidebar />
    <AppTopMenu v-if="topMenu" />
    <div class="flex min-h-0 flex-1 flex-col overflow-hidden">
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

    <!-- Overlays opened from the header (⚙, "Go to…", Ctrl K); mounted once for the whole shell. -->
    <AppearanceSettings />
    <CommandPalette />
  </div>
</template>
