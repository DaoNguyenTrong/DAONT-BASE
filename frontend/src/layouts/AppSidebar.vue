<script setup lang="ts">
import type { SidebarMenuItem } from './sidebar-menu'
import AppSidebarItem from './AppSidebarItem.vue'
import ProfileDialog from '@/components/ProfileDialog.vue'

const { t } = useI18n()
const router = useRouter()
const route = useRoute()
const sidebar = useSidebarStore()
const auth = useAuthStore()
const sidebarMenu = useSidebarMenu()
const { wordmarkSrc } = useBrandWordmark()

function isActive(item: SidebarMenuItem): boolean {
  if (item.routeName) {
    return route.name === item.routeName
  }
  return false
}

function goHome() {
  sidebar.closeMobile()
  router.push(useHomeRoute())
}

function handleLogout() {
  requestConfirmationNaive({
    message: t('auth.logoutConfirmMessage'),
    header: t('auth.logout'),
    acceptLabel: t('common.confirm'),
    rejectLabel: t('common.cancel'),
    accept: async () => {
      try {
        await useAuthStore().logout()
      } finally {
        sidebar.closeMobile()
        await router.replace({ name: 'login' })
      }
    },
  })
}

const viewProfile = () => {
  profileVisible.value = true
  sidebar.closeMobile()
}

const profileVisible = ref(false)
</script>

<template>
  <!-- Desktop sidebar -->
  <aside
    class="hidden shrink-0 flex-col border-r border-surface-200 bg-surface-0 transition-[width] duration-300 ease-in-out dark:border-surface-800 dark:bg-surface-900 lg:flex"
    :style="{ width: sidebar.sidebarWidth }"
  >
    <!-- Logo area -->
    <button
      type="button"
      class="flex h-16 w-full cursor-pointer items-center justify-center border-b border-surface-200 text-left transition-colors hover:bg-surface-100 dark:border-surface-800 dark:hover:bg-surface-800"
      :class="sidebar.isMinimal ? 'justify-center' : 'gap-3'"
      @click="goHome"
    >
      <img
        v-if="sidebar.isMinimal"
        :alt="t('app.name')"
        class="h-9 w-9 object-cover shrink-0"
        src="/weatherplus-mark.webp"
      />
      <img v-else :alt="t('app.name')" class="h-9 shrink-0" :src="wordmarkSrc" />
    </button>

    <!-- Navigation -->
    <nav class="flex-1 overflow-y-auto px-2 py-3">
      <AppSidebarItem
        v-for="item in sidebarMenu"
        :key="item.labelKey"
        :item="item"
        :depth="0"
        :minimal="sidebar.isMinimal"
      />
    </nav>

    <!-- User section -->
    <div class="p-3">
      <div
        class="border-t border-surface-200 dark:border-surface-700 flex items-center p-3 pb-0 transition-all duration-300"
        :class="sidebar.isMinimal ? 'flex-col gap-3' : 'flex-row gap-3'"
      >
        <n-tooltip v-if="sidebar.isMinimal" trigger="hover" placement="right">
          <template #trigger>
            <button
              type="button"
              :aria-label="t('nav.profile')"
              class="flex h-8 w-8 shrink-0 cursor-pointer items-center justify-center rounded-full bg-surface-200 transition-colors hover:bg-primary-100 dark:bg-surface-600 dark:hover:bg-primary-400/20"
              @click="viewProfile()"
            >
              <SvgIcon name="user" class="text-sm text-surface-600 dark:text-surface-300" />
            </button>
          </template>
          {{ t('nav.profile') }}
        </n-tooltip>
        <button
          v-else
          type="button"
          :aria-label="t('nav.profile')"
          class="flex h-8 w-8 shrink-0 cursor-pointer items-center justify-center rounded-full bg-surface-200 transition-colors hover:bg-primary-100 dark:bg-surface-600 dark:hover:bg-primary-400/20"
          @click="viewProfile()"
        >
          <SvgIcon name="user" class="text-sm text-surface-600 dark:text-surface-300" />
        </button>

        <button
          v-if="!sidebar.isMinimal"
          type="button"
          class="flex-1 cursor-pointer overflow-hidden text-left"
          @click="viewProfile()"
        >
          <p
            class="truncate text-sm font-medium text-surface-700 hover:text-primary-600 dark:text-surface-200 dark:hover:text-primary-200"
          >
            {{ auth.account?.name || auth.account?.username || 'User' }}
          </p>
        </button>
        <n-tooltip trigger="hover" placement="top">
          <template #trigger>
            <n-button
              type="error"
              ghost
              :aria-label="t('auth.logout')"
              class="h-8 w-8 cursor-pointer"
              @click="handleLogout"
            >
              <template #icon><SvgIcon name="sign-out" class="text-sm" /></template>
            </n-button>
          </template>
          {{ t('auth.logout') }}
        </n-tooltip>
      </div>
    </div>
  </aside>

  <ProfileDialog v-model:visible="profileVisible" />

  <!-- Mobile sidebar -->
  <n-drawer v-model:show="sidebar.mobileOpen" placement="left" :width="280" class="lg:hidden!">
    <n-drawer-content>
      <template #header>
        <button
          type="button"
          class="flex w-full cursor-pointer items-center gap-3 text-left"
          @click="goHome"
        >
          <img :alt="t('app.name')" class="h-8 w-auto shrink-0" :src="wordmarkSrc" />
        </button>
      </template>

      <nav class="px-1 py-2">
        <AppSidebarItem
          v-for="item in sidebarMenu"
          :key="item.labelKey"
          :item="item"
          :depth="0"
          :mobile="true"
        />
      </nav>

      <template #footer>
        <div
          class="flex w-full items-center gap-2 border-t border-surface-200 px-4 py-3 dark:border-surface-800"
        >
          <button
            type="button"
            :aria-label="t('nav.profile')"
            class="flex h-8 w-8 shrink-0 cursor-pointer items-center justify-center rounded-full bg-surface-200 transition-colors hover:bg-primary-100 dark:bg-surface-600 dark:hover:bg-primary-400/20"
            @click="viewProfile()"
          >
            <SvgIcon name="user" class="text-sm text-surface-600 dark:text-surface-300" />
          </button>
          <button
            type="button"
            class="flex-1 cursor-pointer overflow-hidden text-left"
            @click="viewProfile()"
          >
            <p
              class="truncate text-sm font-medium text-surface-700 hover:text-primary-600 dark:text-surface-200 dark:hover:text-primary-200"
            >
              {{ auth.account?.name || auth.account?.username || 'User' }}
            </p>
          </button>
          <n-button
            type="error"
            text
            :aria-label="t('auth.logout')"
            class="h-8 w-8 cursor-pointer"
            @click="handleLogout"
          >
            <template #icon><SvgIcon name="sign-out" class="text-sm" /></template>
          </n-button>
        </div>
      </template>
    </n-drawer-content>
  </n-drawer>
</template>
