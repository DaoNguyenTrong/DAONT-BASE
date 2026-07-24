import type { ThemePreference } from '@/lib/theme'

const isDark = ref(getThemePreference() === 'dark')

function toggle() {
  isDark.value = toggleThemePreference() === 'dark'
}

function setPreference(theme: ThemePreference) {
  isDark.value = setThemePreference(theme) === 'dark'
}

export function useThemePreference() {
  return { isDark, toggle, setPreference }
}
