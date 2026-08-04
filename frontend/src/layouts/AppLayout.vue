<script setup lang="ts">
import AppFooter from './AppFooter.vue'
import AppHeader from './AppHeader.vue'
import AppSidebar from './AppSidebar.vue'

const notificationsStore = useNotificationsStore()

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
  <div class="flex h-screen overflow-hidden bg-surface-100 dark:bg-surface-950">
    <AppSidebar />
    <div class="flex flex-1 flex-col overflow-hidden">
      <AppHeader />
      <main class="flex-1 overflow-y-auto p-4 lg:p-6">
        <RouterView />
      </main>
      <AppFooter />
    </div>
  </div>
</template>
