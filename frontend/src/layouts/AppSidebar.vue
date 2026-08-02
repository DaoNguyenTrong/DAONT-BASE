<script setup lang="ts">
import { Logout, User } from '@vicons/tabler'
import type { SidebarMenuItem } from './sidebar-menu'
import AppSidebarItem from './AppSidebarItem.vue'
import ProfileDialog from '@/components/ProfileDialog.vue'
import OrganizationSwitcher from '@/components/OrganizationSwitcher.vue'

const { t } = useI18n()
const router = useRouter()
const route = useRoute()
const sidebar = useSidebarStore()
const auth = useAuthStore()
const sidebarMenu = useSidebarMenu()
const { isDark } = useThemePreference()

// Sidebar background is always dark-family (purple in light mode, navy in
// dark mode — see AppSidebar's aside class), so it always needs the
// white-text wordmark regardless of overall theme, unlike every other
// wordmark usage in the app which still follows isDark.
const sidebarWordmarkSrc = '/weatherplus-wordmark-dark.webp'

// Scoped to just the sidebar (desktop aside + mobile drawer, wrapped below —
// deliberately NOT ProfileDialog, which sits on a white modal regardless of
// theme and needs the normal errorColor) via Naive UI's local theme-overrides
// pattern, rather than the global Drawer/popoverColor/errorColor tokens,
// which are shared by every popover/dropdown/destructive-action app-wide.
const sidebarThemeOverrides = computed(() =>
  isDark.value
    ? undefined
    : {
        common: {
          // Naive UI's default errorColor (#d03050) is a mid-tone red that
          // reads at ~1.4:1 contrast against the #5e4c9e sidebar — barely
          // visible ("chìm"). Lightened to a pale rose that still clears
          // WCAG AA (~5:1) against the sidebar without losing the
          // destructive-action red hue.
          errorColor: '#fecdd3',
          errorColorHover: '#ffe4e6',
          errorColorPressed: '#fda4af',
          errorColorSuppl: '#fecdd3',
        },
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
      },
)

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
  <n-config-provider abstract :theme-overrides="sidebarThemeOverrides">
    <aside
      class="hidden shrink-0 flex-col border-r border-white/10 bg-primary-600 transition-[width] duration-300 ease-in-out dark:border-surface-800 dark:bg-surface-900 lg:flex"
      :style="{ width: sidebar.sidebarWidth }"
    >
      <!-- Logo area -->
      <button
        type="button"
        class="flex h-16 w-full cursor-pointer items-center justify-center border-b border-white/10 text-left transition-colors hover:bg-white/10 dark:border-surface-800 dark:hover:bg-surface-800"
        :class="sidebar.isMinimal ? 'justify-center' : 'gap-3'"
        @click="goHome"
      >
        <img
          v-if="sidebar.isMinimal"
          :alt="t('app.name')"
          class="h-9 w-9 object-cover shrink-0"
          src="/weatherplus-mark.webp"
        />
        <img v-else :alt="t('app.name')" class="h-9 shrink-0" :src="sidebarWordmarkSrc" />
      </button>

      <!-- Navigation -->
      <nav class="flex-1 overflow-y-auto px-3 py-3">
        <AppSidebarItem
          v-for="item in sidebarMenu"
          :key="item.labelKey"
          :item="item"
          :depth="0"
          :minimal="sidebar.isMinimal"
        />
      </nav>

      <!-- Organization switcher -->
      <div class="px-3 pb-1" :class="sidebar.isMinimal ? 'flex justify-center' : ''">
        <OrganizationSwitcher :minimal="sidebar.isMinimal" />
      </div>

      <!-- User section -->
      <div class="p-3">
        <div
          class="border-t border-white/10 dark:border-surface-700 flex items-center p-3 pb-0 transition-all duration-300"
          :class="sidebar.isMinimal ? 'flex-col gap-3' : 'flex-row gap-3'"
        >
          <n-tooltip v-if="sidebar.isMinimal" trigger="hover" placement="right">
            <template #trigger>
              <button
                type="button"
                :aria-label="t('nav.profile')"
                class="flex h-8 w-8 shrink-0 cursor-pointer items-center justify-center rounded-full bg-white/10 transition-colors hover:bg-white/20 dark:bg-surface-600 dark:hover:bg-primary-400/20"
                @click="viewProfile()"
              >
                <n-icon class="text-sm text-white/80 dark:text-surface-300"><User /></n-icon>
              </button>
            </template>
            {{ t('nav.profile') }}
          </n-tooltip>
          <button
            v-else
            type="button"
            :aria-label="t('nav.profile')"
            class="flex h-8 w-8 shrink-0 cursor-pointer items-center justify-center rounded-full bg-white/10 transition-colors hover:bg-white/20 dark:bg-surface-600 dark:hover:bg-primary-400/20"
            @click="viewProfile()"
          >
            <n-icon class="text-sm text-white/80 dark:text-surface-300"><User /></n-icon>
          </button>

          <button
            v-if="!sidebar.isMinimal"
            type="button"
            class="flex-1 cursor-pointer overflow-hidden text-left"
            @click="viewProfile()"
          >
            <p
              class="truncate text-sm font-medium text-white hover:text-white/70 dark:text-surface-200 dark:hover:text-primary-200"
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
                class="cursor-pointer"
                @click="handleLogout"
              >
                <template #icon
                  ><n-icon class="text-sm"><Logout /></n-icon
                ></template>
              </n-button>
            </template>
            {{ t('auth.logout') }}
          </n-tooltip>
        </div>
      </div>
    </aside>
  </n-config-provider>

  <ProfileDialog v-model:visible="profileVisible" />

  <!-- Mobile sidebar -->
  <n-config-provider abstract :theme-overrides="sidebarThemeOverrides">
    <n-drawer v-model:show="sidebar.mobileOpen" placement="left" :width="280" class="lg:hidden!">
      <n-drawer-content>
        <template #header>
          <button
            type="button"
            class="flex w-full cursor-pointer items-center gap-3 text-left"
            @click="goHome"
          >
            <img :alt="t('app.name')" class="h-8 w-auto shrink-0" :src="sidebarWordmarkSrc" />
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
          <div class="border-t border-white/10 px-4 py-2 dark:border-surface-800">
            <OrganizationSwitcher />
          </div>
          <div
            class="flex w-full items-center gap-2 border-t border-white/10 px-4 py-3 dark:border-surface-800"
          >
            <button
              type="button"
              :aria-label="t('nav.profile')"
              class="flex h-8 w-8 shrink-0 cursor-pointer items-center justify-center rounded-full bg-white/10 transition-colors hover:bg-white/20 dark:bg-surface-600 dark:hover:bg-primary-400/20"
              @click="viewProfile()"
            >
              <n-icon class="text-sm text-white/80 dark:text-surface-300"><User /></n-icon>
            </button>
            <button
              type="button"
              class="flex-1 cursor-pointer overflow-hidden text-left"
              @click="viewProfile()"
            >
              <p
                class="truncate text-sm font-medium text-white hover:text-white/70 dark:text-surface-200 dark:hover:text-primary-200"
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
              <template #icon
                ><n-icon class="text-sm"><Logout /></n-icon
              ></template>
            </n-button>
          </div>
        </template>
      </n-drawer-content>
    </n-drawer>
  </n-config-provider>
</template>
