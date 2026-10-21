<script setup lang="ts">
import { Check, ChevronDown, Language, Logout, User } from '@vicons/tabler'
import { resolveAssetUrl } from '@/lib/asset-url'
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
const triggerEl = ref<HTMLElement | null>(null)
const menuEl = ref<HTMLElement | null>(null)

const displayName = computed(() => auth.account?.name || auth.account?.username || t('common.user'))
const initials = computed(() => initialsOf(displayName.value))
const avatarUrl = computed(() => resolveAssetUrl(auth.account?.avatarUrl))
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

// Keyboard access (WAI-ARIA menu pattern): the popover traps no focus and is teleported to the end of
// <body>, so focus is moved in on open, roved with the arrow keys, and handed back to the trigger on close.
function menuItems() {
  return Array.from(
    menuEl.value?.querySelectorAll<HTMLElement>('[role="menuitem"], [role="menuitemradio"]') ?? [],
  )
}

watch(open, async (isOpen) => {
  if (isOpen) {
    await nextTick()
    menuItems()[0]?.focus()
  } else if (menuEl.value?.contains(document.activeElement)) {
    // Closed by an item (profile, logout, an organization): focus would otherwise fall to <body>.
    // A click elsewhere has already moved focus out, so it is left alone.
    triggerEl.value?.focus()
  }
})

function closeToTrigger() {
  triggerEl.value?.focus()
  close()
}

function onMenuKeydown(event: KeyboardEvent) {
  const items = menuItems()
  const index = items.indexOf(document.activeElement as HTMLElement)
  let next: HTMLElement | undefined

  switch (event.key) {
    case 'ArrowDown':
      next = items[(index + 1) % items.length]
      break
    case 'ArrowUp':
      next = items[index <= 0 ? items.length - 1 : index - 1]
      break
    case 'Home':
      next = items[0]
      break
    case 'End':
      next = items[items.length - 1]
      break
    case 'Escape':
      // Prevented, so the drawer's window-level Esc leaves its own state alone.
      event.preventDefault()
      closeToTrigger()
      return
    case 'Tab':
      // Not prevented: focus is back on the trigger before the browser acts, so Tab moves on from there.
      closeToTrigger()
      return
    default:
      return
  }

  event.preventDefault()
  next?.focus()
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
        ref="triggerEl"
        type="button"
        :aria-label="t('layout.userMenu.open')"
        aria-haspopup="menu"
        :aria-expanded="open"
        class="flex cursor-pointer items-center gap-2 rounded-lg p-1 transition-colors"
        :class="
          onBrand ? 'text-white hover:bg-white/10' : 'hover:bg-black/5 dark:hover:bg-white/10'
        "
        data-testid="app-header-user-menu"
      >
        <span
          data-testid="user-menu-avatar"
          class="flex h-8 w-8 shrink-0 items-center justify-center overflow-hidden rounded-full text-xs font-semibold text-white"
          :class="onBrand ? 'bg-white/20' : 'bg-primary-600'"
          aria-hidden="true"
        >
          <img v-if="avatarUrl" :src="avatarUrl" alt="" class="h-full w-full object-cover" />
          <template v-else>{{ initials }}</template>
        </span>
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

    <div ref="menuEl" class="flex flex-col gap-0.5" role="menu" @keydown="onMenuKeydown">
      <div class="flex items-center gap-3 px-2 py-1.5">
        <span
          data-testid="user-menu-panel-avatar"
          class="flex h-10 w-10 shrink-0 items-center justify-center overflow-hidden rounded-full bg-primary-600 text-sm font-semibold text-white"
          aria-hidden="true"
        >
          <img v-if="avatarUrl" :src="avatarUrl" alt="" class="h-full w-full object-cover" />
          <template v-else>{{ initials }}</template>
        </span>
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
</template>
