<script setup lang="ts">
import { Minus, Moon, Plus, Refresh, Sun, World } from '@vicons/tabler'

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
            <n-icon class="h-3 w-3"><component :is="isDark ? Moon : Sun" /></n-icon>
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
          <template #icon
            ><n-icon><Plus /></n-icon
          ></template>
          {{ t('common.increaseFontSize') }}
        </n-button>
        <n-button text :disabled="!canDecrease" @click="decreaseFontSize">
          <template #icon
            ><n-icon><Minus /></n-icon
          ></template>
          {{ t('common.decreaseFontSize') }}
        </n-button>
        <n-divider class="my-1!" />
        <n-button text :disabled="!canReset" @click="resetFontSize">
          <template #icon
            ><n-icon><Refresh /></n-icon
          ></template>
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
            <n-icon class="h-3 w-3"><World /></n-icon>
          </template>
        </n-button>
      </template>
      {{ `${t('common.language')}: ${currentLocale === 'en' ? 'VI' : 'EN'}` }}
    </n-tooltip>
  </n-button-group>
</template>
