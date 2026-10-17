import type { ThemePreference } from '@/lib/theme'

const preference = ref<ThemePreference>(getThemeSetting())
const isDark = ref(getThemePreference() === 'dark')

watchSystemTheme((theme) => {
  isDark.value = theme === 'dark'
})

function toggle() {
  isDark.value = toggleThemePreference() === 'dark'
  preference.value = getThemeSetting()
}

function setPreference(next: ThemePreference) {
  isDark.value = setThemePreference(next) === 'dark'
  preference.value = next
}

export function useThemePreference() {
  return { isDark, preference, toggle, setPreference }
}
