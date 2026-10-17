<script setup lang="ts">
import { Minus, Plus, Refresh } from '@vicons/tabler'
import { MENU_MODES, MENU_THEMES, type MenuMode } from '@/lib/layout-preferences'
import { BELOW_LG_QUERY } from '@/lib/responsive'
import type { ThemePreference } from '@/lib/theme'
import MenuModePreview from './MenuModePreview.vue'

const { t } = useI18n()
const layout = useLayoutStore()
const { settingsOpen, menuMode, menuTheme } = storeToRefs(layout)
const { preference, setPreference } = useThemePreference()
const belowLg = useMediaQuery(BELOW_LG_QUERY)

const COLOR_MODES: readonly ThemePreference[] = ['light', 'dark', 'system']

// Mode values are kebab-case, locale keys camelCase.
const MODE_LABEL_KEYS: Record<MenuMode, string> = {
  static: 'layout.menuModes.static',
  slim: 'layout.menuModes.slim',
  'slim-plus': 'layout.menuModes.slimPlus',
  drawer: 'layout.menuModes.drawer',
  horizontal: 'layout.menuModes.horizontal',
}

// Font size (same behaviour as AppControls)
const fontSize = ref(getFontSize())
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

const segmentClass = (active: boolean) => [
  'flex-1 rounded-md border px-3 py-1.5 text-sm transition-colors pointer-coarse:min-h-10',
  active
    ? 'border-primary-600 bg-primary-600/10 text-primary-600 dark:border-primary-300 dark:text-primary-300 font-semibold'
    : 'border-surface-300 dark:border-surface-600 hover:bg-surface-100 dark:hover:bg-surface-800',
]
</script>

<template>
  <n-drawer v-model:show="settingsOpen" placement="right" :width="320">
    <n-drawer-content :title="t('layout.settings.title')" closable>
      <div class="flex flex-col gap-6">
        <section :aria-label="t('layout.settings.colorMode')">
          <h3 class="mb-2 text-sm font-semibold">{{ t('layout.settings.colorMode') }}</h3>
          <div class="flex gap-2">
            <button
              v-for="option in COLOR_MODES"
              :key="option"
              type="button"
              :data-testid="`appearance-theme-${option}`"
              :aria-pressed="preference === option"
              :class="segmentClass(preference === option)"
              @click="setPreference(option)"
            >
              {{ t(`layout.settings.${option}`) }}
            </button>
          </div>
        </section>

        <section
          :aria-label="t('layout.settings.menuMode')"
          :aria-disabled="belowLg"
          :class="{ 'opacity-60': belowLg }"
        >
          <h3 class="mb-2 text-sm font-semibold">{{ t('layout.settings.menuMode') }}</h3>
          <p v-if="belowLg" id="appearance-mode-note" class="text-surface-500 mb-2 text-xs">
            {{ t('layout.settings.menuModeWideOnly') }}
          </p>
          <div class="grid grid-cols-2 gap-2">
            <button
              v-for="mode in MENU_MODES"
              :key="mode"
              type="button"
              :data-testid="`appearance-mode-${mode}`"
              :aria-pressed="menuMode === mode"
              :aria-describedby="belowLg ? 'appearance-mode-note' : undefined"
              :class="[
                'flex flex-col gap-1.5 rounded-md border p-2 text-left text-xs transition-colors pointer-coarse:min-h-10',
                menuMode === mode
                  ? 'border-primary-600 bg-primary-600/10 dark:border-primary-300 font-semibold'
                  : 'border-surface-300 dark:border-surface-600 hover:bg-surface-100 dark:hover:bg-surface-800',
              ]"
              @click="layout.setMenuMode(mode)"
            >
              <MenuModePreview :mode="mode" />
              {{ t(MODE_LABEL_KEYS[mode]) }}
            </button>
          </div>
        </section>

        <section :aria-label="t('layout.settings.menuTheme')">
          <h3 class="mb-2 text-sm font-semibold">{{ t('layout.settings.menuTheme') }}</h3>
          <div class="flex gap-2">
            <button
              v-for="theme in MENU_THEMES"
              :key="theme"
              type="button"
              :data-testid="`appearance-menu-theme-${theme}`"
              :aria-pressed="menuTheme === theme"
              :class="segmentClass(menuTheme === theme)"
              @click="layout.setMenuTheme(theme)"
            >
              {{ t(`layout.menuThemes.${theme}`) }}
            </button>
          </div>
        </section>

        <section :aria-label="t('common.fontSize')">
          <h3 class="mb-2 text-sm font-semibold">{{ t('common.fontSize') }}</h3>
          <div class="flex items-center gap-2">
            <n-button
              secondary
              class="pointer-coarse:h-10! pointer-coarse:min-w-10!"
              :disabled="!canDecrease"
              :aria-label="t('common.decreaseFontSize')"
              @click="decreaseFontSize"
            >
              <template #icon
                ><n-icon><Minus /></n-icon
              ></template>
            </n-button>
            <span class="min-w-12 text-center text-sm tabular-nums">{{ fontSize }}px</span>
            <n-button
              secondary
              class="pointer-coarse:h-10! pointer-coarse:min-w-10!"
              :disabled="!canIncrease"
              :aria-label="t('common.increaseFontSize')"
              @click="increaseFontSize"
            >
              <template #icon
                ><n-icon><Plus /></n-icon
              ></template>
            </n-button>
            <n-button
              text
              class="pointer-coarse:h-10! pointer-coarse:min-w-10!"
              :disabled="!canReset"
              :aria-label="t('common.resetFontSize')"
              @click="resetFontSize"
            >
              <template #icon
                ><n-icon><Refresh /></n-icon
              ></template>
            </n-button>
          </div>
        </section>
      </div>
    </n-drawer-content>
  </n-drawer>
</template>
