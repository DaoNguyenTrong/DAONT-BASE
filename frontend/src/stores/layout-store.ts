import {
  readLayoutPreferences,
  writeLayoutPreference,
  type MenuMode,
  type MenuTheme,
} from '@/lib/layout-preferences'

export const useLayoutStore = defineStore('layout', () => {
  const initial = readLayoutPreferences()

  // Persisted preferences
  const menuMode = ref<MenuMode>(initial.menuMode)
  const menuTheme = ref<MenuTheme>(initial.menuTheme)
  const drawerPinned = ref(initial.drawerPinned)
  const staticInactive = ref(initial.staticInactive)

  // Session-only state
  const mobileOpen = ref(false)
  const drawerExpanded = ref(false)
  const settingsOpen = ref(false)
  const paletteOpen = ref(false)
  const profileOpen = ref(false)

  // Getters
  const isRail = computed(
    () =>
      menuMode.value === 'slim' ||
      menuMode.value === 'slim-plus' ||
      (menuMode.value === 'drawer' && !drawerPinned.value),
  )
  const railWidth = computed(() => (menuMode.value === 'slim-plus' ? '6rem' : '4rem'))

  // Actions
  function setMenuMode(mode: MenuMode) {
    menuMode.value = mode
    writeLayoutPreference('menuMode', mode)
    drawerExpanded.value = false
    mobileOpen.value = false
  }

  function setMenuTheme(theme: MenuTheme) {
    menuTheme.value = theme
    writeLayoutPreference('menuTheme', theme)
  }

  function toggleStaticSidebar() {
    staticInactive.value = !staticInactive.value
    writeLayoutPreference('staticInactive', staticInactive.value)
  }

  function setDrawerPinned(value: boolean) {
    drawerPinned.value = value
    writeLayoutPreference('drawerPinned', value)
    drawerExpanded.value = false
  }

  function expandDrawer() {
    drawerExpanded.value = true
  }
  function collapseDrawer() {
    drawerExpanded.value = false
  }
  function openMobile() {
    mobileOpen.value = true
  }
  function closeMobile() {
    mobileOpen.value = false
  }
  function openSettings() {
    settingsOpen.value = true
  }
  function closeSettings() {
    settingsOpen.value = false
  }
  function openPalette() {
    paletteOpen.value = true
  }
  function closePalette() {
    paletteOpen.value = false
  }
  function openProfile() {
    profileOpen.value = true
  }
  function closeProfile() {
    profileOpen.value = false
  }

  return {
    menuMode,
    menuTheme,
    drawerPinned,
    staticInactive,
    mobileOpen,
    drawerExpanded,
    settingsOpen,
    paletteOpen,
    profileOpen,
    isRail,
    railWidth,
    setMenuMode,
    setMenuTheme,
    toggleStaticSidebar,
    setDrawerPinned,
    expandDrawer,
    collapseDrawer,
    openMobile,
    closeMobile,
    openSettings,
    closeSettings,
    openPalette,
    closePalette,
    openProfile,
    closeProfile,
  }
})
