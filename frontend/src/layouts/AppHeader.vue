<script setup lang="ts">
import { Home, Menu2 } from '@vicons/tabler'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const sidebar = useSidebarStore()

function handleToggle() {
  if (window.innerWidth >= 1024) {
    sidebar.toggleMode()
  } else {
    sidebar.openMobile()
  }
}

type BreadcrumbEntry = { routeName: string; label?: string; home?: boolean }

const breadcrumbItems = computed<BreadcrumbEntry[]>(() => [
  { routeName: 'home', home: true },
  ...route.matched
    .filter((r) => r.meta.breadcrumbKey)
    .map((r) => ({
      routeName: r.name as string,
      label: t(r.meta.breadcrumbKey as string),
    })),
])

function breadcrumbHref(routeName: string) {
  return router.resolve({ name: routeName, params: route.params }).href
}

function navigateTo(routeName: string, event: MouseEvent) {
  event.preventDefault()
  router.push({ name: routeName, params: route.params })
}
</script>

<template>
  <header
    class="sticky top-0 z-10 flex h-16 items-center gap-3 border-b border-surface-200 bg-surface-0/80 px-4 backdrop-blur-sm dark:border-surface-800 dark:bg-surface-900/80"
  >
    <n-button
      :aria-label="t('nav.toggleSidebar')"
      text
      class="h-9 w-9 lg:h-8 lg:w-8"
      @click="handleToggle"
    >
      <template #icon
        ><n-icon><Menu2 /></n-icon
      ></template>
    </n-button>

    <n-breadcrumb class="hidden! lg:flex! breadcrumb-align-fix">
      <n-breadcrumb-item
        v-for="item in breadcrumbItems"
        :key="item.routeName"
        :href="breadcrumbHref(item.routeName)"
        @click="navigateTo(item.routeName, $event)"
      >
        <n-icon v-if="item.home"><Home /></n-icon>
        <template v-else>{{ item.label }}</template>
      </n-breadcrumb-item>
    </n-breadcrumb>

    <div class="flex-1" />

    <NotificationBell />
    <AppControls />
  </header>
</template>

<style scoped>
.breadcrumb-align-fix :deep(> ul) {
  display: flex;
  align-items: center;
}
</style>
