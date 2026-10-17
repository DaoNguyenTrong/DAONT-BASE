<script setup lang="ts">
import { Check, ChevronDown, Language, Logout, User } from '@vicons/tabler'
import { initialsOf } from '@/lib/initials'
import type { SupportedLocale } from '@/plugins/i18n'

// 'on-brand' is the trigger on the purple menu bar (horizontal mode, brand color, light): white tones there.
const props = withDefaults(defineProps<{ tone?: 'default' | 'on-brand' }>(), { tone: 'default' })
const onBrand = computed(() => props.tone === 'on-brand')

const { t } = useI18n()
const auth = useAuthStore()
const layout = useLayoutStore()
const localeStore = useLocaleStore()
const confirmLogout = useConfirmLogout()

const open = ref(false)

const displayName = computed(() => auth.account?.name || auth.account?.username || t('common.user'))
const initials = computed(() => initialsOf(displayName.value))
const organizationLabel = computed(
  () => auth.organizationName ?? t('organizations.personalWorkspace'),
)

const locales = computed<{ code: SupportedLocale; label: string }[]>(() => [
  { code: 'vi', label: t('layout.userMenu.vietnamese') },
  { code: 'en', label: t('layout.userMenu.english') },
])

function close() {
  open.value = false
}

function viewProfile() {
  close()
  layout.openProfile()
}

function logout() {
  close()
  confirmLogout()
}

const itemClass =
  'flex w-full cursor-pointer items-center gap-2 rounded-lg px-2 py-1.5 text-left text-sm transition-colors hover:bg-surface-100 dark:hover:bg-surface-800'
</script>

<template>
  <n-popover v-model:show="open" trigger="click" placement="bottom-end" :width="260">
    <template #trigger>
      <button
        type="button"
        :aria-label="t('layout.userMenu.open')"
        class="flex cursor-pointer items-center gap-2 rounded-lg p-1 transition-colors"
        :class="
          onBrand ? 'text-white hover:bg-white/10' : 'hover:bg-black/5 dark:hover:bg-white/10'
        "
        data-testid="app-header-user-menu"
      >
        <span
          data-testid="user-menu-avatar"
          class="flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-xs font-semibold text-white"
          :class="onBrand ? 'bg-white/20' : 'bg-primary-600'"
          aria-hidden="true"
          >{{ initials }}</span
        >
        <span class="hidden min-w-0 max-w-40 flex-col text-left leading-tight xl:flex">
          <span class="truncate text-sm font-medium" :title="displayName">{{ displayName }}</span>
          <span
            data-testid="user-menu-organization"
            class="truncate text-xs"
            :class="onBrand ? 'text-white/70' : 'text-surface-500'"
            :title="organizationLabel"
            >{{ organizationLabel }}</span
          >
        </span>
        <n-icon
          data-testid="user-menu-chevron"
          class="hidden shrink-0 text-sm xl:block"
          :class="onBrand ? 'text-white/70' : 'text-surface-400'"
          ><ChevronDown
        /></n-icon>
      </button>
    </template>

    <div class="flex flex-col gap-0.5" role="menu">
      <div class="flex items-center gap-3 px-2 py-1.5">
        <span
          class="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-primary-600 text-sm font-semibold text-white"
          aria-hidden="true"
          >{{ initials }}</span
        >
        <div class="flex min-w-0 flex-col leading-tight">
          <span class="truncate text-sm font-medium">{{ displayName }}</span>
          <span class="truncate text-xs text-surface-500">{{ auth.account?.email }}</span>
        </div>
      </div>

      <n-divider class="my-1!" />

      <OrganizationList @done="close" />

      <n-divider class="my-1!" />

      <button
        type="button"
        role="menuitem"
        :class="itemClass"
        data-testid="user-menu-profile"
        @click="viewProfile"
      >
        <n-icon class="shrink-0"><User /></n-icon>
        <span>{{ t('nav.profile') }}</span>
      </button>

      <div class="flex items-center gap-2 px-2 pt-1.5 text-xs text-surface-500">
        <n-icon class="shrink-0"><Language /></n-icon>
        <span>{{ t('common.language') }}</span>
      </div>
      <button
        v-for="locale in locales"
        :key="locale.code"
        type="button"
        role="menuitemradio"
        :aria-checked="localeStore.currentLocale === locale.code"
        :class="itemClass"
        :data-testid="`user-menu-locale-${locale.code}`"
        @click="localeStore.setLocale(locale.code)"
      >
        <n-icon class="shrink-0"><Check v-if="localeStore.currentLocale === locale.code" /></n-icon>
        <span>{{ locale.label }}</span>
      </button>

      <n-divider class="my-1!" />

      <button
        type="button"
        role="menuitem"
        :class="[itemClass, 'text-error-600! dark:text-error-400!']"
        data-testid="user-menu-logout"
        @click="logout"
      >
        <n-icon class="shrink-0"><Logout /></n-icon>
        <span>{{ t('auth.logout') }}</span>
      </button>
    </div>
  </n-popover>

  <ProfileDialog v-model:visible="layout.profileOpen" />
</template>
