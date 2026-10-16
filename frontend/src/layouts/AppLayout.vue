<script setup lang="ts">
import AppFooter from './AppFooter.vue'
import AppHeader from './AppHeader.vue'
import AppSidebar from './AppSidebar.vue'

const notificationsStore = useNotificationsStore()
const route = useRoute()

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
  <div class="relative flex h-dvh overflow-hidden bg-surface-100 dark:bg-surface-950">
    <AppSidebar />
    <div class="flex flex-1 flex-col overflow-hidden">
      <AppHeader />
      <main
        ref="mainEl"
        class="relative flex-1 overflow-y-auto p-4 [scrollbar-gutter:stable] lg:p-6"
      >
        <RouterView />
      </main>
      <AppFooter />
    </div>
  </div>
</template>
