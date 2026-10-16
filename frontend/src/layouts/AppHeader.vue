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
      class="h-9 w-9 shrink-0 max-lg:h-11! max-lg:w-11! lg:h-8 lg:w-8"
      @click="handleToggle"
    >
      <template #icon
        ><n-icon><Menu2 /></n-icon
      ></template>
    </n-button>

    <n-breadcrumb class="hidden! min-w-0 lg:flex! breadcrumb-align-fix">
      <n-breadcrumb-item
        v-for="item in breadcrumbItems"
        :key="item.routeName"
        :href="breadcrumbHref(item.routeName)"
        @click="navigateTo(item.routeName, $event)"
      >
        <n-icon v-if="item.home"><Home /></n-icon>
        <!-- A long label is cut with "…" so the trail stays on one line; `title` keeps the full text. -->
        <span v-else data-testid="app-header-crumb-text" :title="item.label">{{ item.label }}</span>
      </n-breadcrumb-item>
    </n-breadcrumb>

    <div class="flex-1" />

    <!-- On phones the bell goes rightmost, so its panel (anchored to the bell's right edge) has the whole
         viewport width to open into instead of running off the left edge. -->
    <div class="flex max-sm:order-2">
      <NotificationBell />
    </div>
    <AppControls class="max-sm:order-1" />
  </header>
</template>

<style scoped>
/*
 * The trail stays on one line instead of pushing the header out of shape when a crumb carries a long name
 * (an entity title in a fork). Every crumb has a width cap, even with room to spare, so one long name can't
 * take the whole row; narrower still, the last crumb (the current page) gives way first. Separators don't shrink.
 */
.breadcrumb-align-fix :deep(> ul) {
  display: flex;
  align-items: center;
  flex-wrap: nowrap;
  min-width: 0;
  overflow: hidden;
}

.breadcrumb-align-fix :deep(.n-breadcrumb-item) {
  display: flex;
  align-items: center;
  min-width: 0;
  max-width: 12rem;
  flex-shrink: 1;
}

.breadcrumb-align-fix :deep(.n-breadcrumb-item:last-child) {
  max-width: 18rem;
  flex-shrink: 1000;
}

.breadcrumb-align-fix :deep(.n-breadcrumb-item__link) {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.breadcrumb-align-fix :deep(.n-breadcrumb-item__separator) {
  flex-shrink: 0;
}
</style>
