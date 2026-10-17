<script setup lang="ts">
import { Home } from '@vicons/tabler'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()

type BreadcrumbEntry = { routeName: string; label?: string; home?: boolean }

// One entry per matched route that declares a breadcrumbKey; the deepest is the page itself.
const levels = computed(() =>
  route.matched
    .filter((r) => r.meta.breadcrumbKey)
    .map((r) => ({ routeName: r.name as string, label: t(r.meta.breadcrumbKey as string) })),
)

const title = computed(() => levels.value.at(-1))

// The home icon leads every trail except home's own, where only the title is left.
const breadcrumbItems = computed<BreadcrumbEntry[]>(() => {
  if (route.name === 'home') return []
  return [{ routeName: 'home', home: true }, ...levels.value.slice(0, -1)]
})

function breadcrumbHref(routeName: string) {
  return router.resolve({ name: routeName, params: route.params }).href
}

function navigateTo(routeName: string, event: MouseEvent) {
  event.preventDefault()
  router.push({ name: routeName, params: route.params })
}
</script>

<template>
  <div class="flex min-w-0 flex-col justify-center leading-tight">
    <!-- Crumbs are for desktop only; below lg the title stands alone. -->
    <n-breadcrumb
      v-if="breadcrumbItems.length"
      class="hidden! min-w-0 lg:flex! breadcrumb-align-fix"
    >
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
    <!-- Not a heading: each page renders its own <h1>. -->
    <p
      v-if="title"
      data-testid="app-header-title"
      class="m-0 truncate text-base font-semibold"
      :title="title.label"
    >
      {{ title.label }}
    </p>
  </div>
</template>

<style scoped>
/*
 * The trail stays on one line instead of pushing the header out of shape when a crumb carries a long name
 * (an entity title in a fork). Every crumb has a width cap, even with room to spare, so one long name can't
 * take the whole row; narrower still, the last crumb gives way first. Separators don't shrink.
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
