<script setup lang="ts">
import { Help, Menu2, Search, Settings } from '@vicons/tabler'
import { BELOW_LG_QUERY } from '@/lib/responsive'
import PageTitle from '@/layouts/PageTitle.vue'
import UserMenu from '@/layouts/UserMenu.vue'

// 'bar' is the full header; 'actions' is only the right-hand cluster, for the horizontal menu's bar.
// `tone` 'on-brand' redraws the cluster in white tones for the purple bar (brand color, light mode).
const props = withDefaults(
  defineProps<{ variant?: 'bar' | 'actions'; tone?: 'default' | 'on-brand' }>(),
  { variant: 'bar', tone: 'default' },
)
const onBrand = computed(() => props.tone === 'on-brand')
const iconColor = computed(() => (onBrand.value ? 'rgba(255, 255, 255, 0.85)' : undefined))

const { t } = useI18n()
const layout = useLayoutStore()
const localeStore = useLocaleStore()
const belowLg = useMediaQuery(BELOW_LG_QUERY)

// Served by nginx next to the app (frontend/user-guide); Vietnamese is the guide's root locale.
const userGuideHref = computed(() =>
  localeStore.currentLocale === 'en' ? '/guide/en/' : '/guide/',
)

// Below lg the toggle opens the mobile drawer in every mode; from lg up only the static sidebar can be hidden.
const showToggle = computed(() => belowLg.value || layout.menuMode === 'static')

function handleToggle() {
  if (belowLg.value) {
    layout.openMobile()
  } else {
    layout.toggleStaticSidebar()
  }
}

const isMac = computed(() => {
  const nav = navigator as Navigator & { userAgentData?: { platform?: string } }
  return /mac/i.test(nav.userAgentData?.platform ?? nav.platform ?? '')
})

const iconButtonClass = 'h-9 w-9 shrink-0 max-lg:h-11! max-lg:w-11! lg:h-8 lg:w-8'
</script>

<template>
  <header
    class="flex items-center gap-3"
    :class="variant === 'bar' && 'sticky top-0 z-10 h-16 px-4 backdrop-blur-sm'"
  >
    <template v-if="variant === 'bar'">
      <n-button
        v-if="showToggle"
        :aria-label="t('nav.toggleSidebar')"
        data-testid="app-header-toggle"
        text
        :class="iconButtonClass"
        @click="handleToggle"
      >
        <template #icon
          ><n-icon><Menu2 /></n-icon
        ></template>
      </n-button>

      <PageTitle class="min-w-0 flex-1" />
    </template>
    <div v-else class="flex-1" />

    <button
      type="button"
      :aria-label="t('layout.palette.trigger')"
      data-testid="app-header-palette"
      class="flex shrink-0 cursor-pointer items-center gap-2 rounded-full border text-sm transition-colors max-md:h-11 max-md:w-11 max-md:justify-center md:h-9 md:px-3"
      :class="
        onBrand
          ? 'border-white/20 bg-white/10 text-white/80 hover:bg-white/15'
          : 'border-surface-200 bg-surface-0/60 text-surface-500 hover:bg-surface-100 dark:border-surface-700 dark:bg-surface-900/60 dark:hover:bg-surface-800'
      "
      @click="layout.openPalette()"
    >
      <n-icon size="16"><Search /></n-icon>
      <span class="hidden md:inline">{{ t('layout.palette.trigger') }}</span>
      <kbd
        class="hidden rounded border px-1.5 text-xs md:inline"
        :class="onBrand ? 'border-white/30' : 'border-surface-200 dark:border-surface-700'"
        >{{ isMac ? '⌘ K' : 'Ctrl K' }}</kbd
      >
    </button>

    <n-tooltip trigger="hover" placement="bottom">
      <template #trigger>
        <n-button
          tag="a"
          :href="userGuideHref"
          target="_blank"
          rel="noopener"
          :aria-label="t('nav.userGuide')"
          data-testid="app-header-user-guide"
          text
          :text-color="iconColor"
          :class="iconButtonClass"
        >
          <template #icon
            ><n-icon><Help /></n-icon
          ></template>
        </n-button>
      </template>
      {{ t('nav.userGuide') }}
    </n-tooltip>

    <!-- On phones the bell goes rightmost, so its panel (anchored to the bell's right edge) has the whole
         viewport width to open into instead of running off the left edge. -->
    <div class="flex max-sm:order-2">
      <NotificationBell :tone="tone" />
    </div>

    <n-button
      :aria-label="t('layout.openSettings')"
      data-testid="app-header-settings"
      text
      :text-color="iconColor"
      :class="[iconButtonClass, 'max-sm:order-1']"
      @click="layout.openSettings()"
    >
      <template #icon
        ><n-icon><Settings /></n-icon
      ></template>
    </n-button>

    <div class="flex max-sm:order-1">
      <UserMenu :tone="tone" />
    </div>
  </header>
</template>
