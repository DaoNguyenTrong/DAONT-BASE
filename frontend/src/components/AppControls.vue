<script setup lang="ts">
const { t } = useI18n()
const localeStore = useLocaleStore()
const { currentLocale } = storeToRefs(localeStore)

// Theme
const { isDark, toggle: toggleTheme } = useThemePreference()
const themeTitle = computed(
  () => `${t('common.theme')}: ${isDark.value ? t('common.lightMode') : t('common.darkMode')}`,
)

// Font size
const fontSize = ref(getFontSize())
const fontMenuShow = ref(false)

const canDecrease = computed(() => fontSize.value > MIN_FONT_SIZE)
const canIncrease = computed(() => fontSize.value < MAX_FONT_SIZE)
const canReset = computed(() => fontSize.value !== DEFAULT_FONT_SIZE)

function decreaseFontSize() {
  fontSize.value = setFontSize(fontSize.value - 1)
}
function increaseFontSize() {
  fontSize.value = setFontSize(fontSize.value + 1)
}
function resetFontSize() {
  fontSize.value = setFontSize(DEFAULT_FONT_SIZE)
}
</script>

<template>
  <n-button-group>
    <n-tooltip trigger="hover" placement="bottom">
      <template #trigger>
        <n-button :aria-label="themeTitle" circle secondary @click="toggleTheme">
          <template #icon>
            <svg
              aria-hidden="true"
              class="h-3 w-3"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              stroke-linecap="round"
              stroke-linejoin="round"
              stroke-width="1.8"
            >
              <path v-if="isDark" d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8Z" />
              <g v-else>
                <circle cx="12" cy="12" r="4" />
                <path
                  d="M12 2.5v2.2M12 19.3v2.2M21.5 12h-2.2M4.7 12H2.5M18.7 5.3l-1.6 1.6M6.9 17.1l-1.6 1.6M18.7 18.7l-1.6-1.6M6.9 6.9 5.3 5.3"
                />
              </g>
            </svg>
          </template>
        </n-button>
      </template>
      {{ themeTitle }}
    </n-tooltip>

    <n-popover v-model:show="fontMenuShow" trigger="click" placement="bottom">
      <template #trigger>
        <n-button
          :aria-label="`${t('common.fontSize')}: ${fontSize}px`"
          aria-haspopup="menu"
          secondary
        >
          <span aria-hidden="true" class="text-sm! font-semibold">A</span>
        </n-button>
      </template>
      <div class="flex min-w-42 flex-col">
        <n-button text :disabled="!canIncrease" @click="increaseFontSize">
          <template #icon><SvgIcon name="plus" /></template>
          {{ t('common.increaseFontSize') }}
        </n-button>
        <n-button text :disabled="!canDecrease" @click="decreaseFontSize">
          <template #icon><SvgIcon name="minus" /></template>
          {{ t('common.decreaseFontSize') }}
        </n-button>
        <n-divider class="my-1!" />
        <n-button text :disabled="!canReset" @click="resetFontSize">
          <template #icon><SvgIcon name="refresh" /></template>
          {{ t('common.resetFontSize') }}
        </n-button>
      </div>
    </n-popover>

    <n-tooltip trigger="hover" placement="bottom">
      <template #trigger>
        <n-button
          :aria-label="`${t('common.language')}: ${currentLocale === 'en' ? 'VI' : 'EN'}`"
          circle
          secondary
          @click="localeStore.toggleLocale"
        >
          <template #icon>
            <svg
              aria-hidden="true"
              class="h-3 w-3"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              stroke-linecap="round"
              stroke-linejoin="round"
              stroke-width="1.8"
            >
              <path d="M12 21c4.97 0 9-4.03 9-9s-4.03-9-9-9-9 4.03-9 9 4.03 9 9 9Z" />
              <path d="M3 12h18M12 3a14.5 14.5 0 0 1 0 18M12 3a14.5 14.5 0 0 0 0 18" />
            </svg>
          </template>
        </n-button>
      </template>
      {{ `${t('common.language')}: ${currentLocale === 'en' ? 'VI' : 'EN'}` }}
    </n-tooltip>
  </n-button-group>
</template>
