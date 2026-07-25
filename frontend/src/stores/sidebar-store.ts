export const useSidebarStore = defineStore('sidebar', () => {
  // State
  const mode = ref<'minimal' | 'full'>(
    (localStorage.getItem('sidebar-mode') as 'minimal' | 'full') || 'full',
  )
  const mobileOpen = ref(false)

  // Getters
  const isMinimal = computed(() => mode.value === 'minimal')
  const sidebarWidth = computed(() => (mode.value === 'minimal' ? '5rem' : '16rem'))

  // Actions
  function toggleMode() {
    mode.value = mode.value === 'minimal' ? 'full' : 'minimal'
    localStorage.setItem('sidebar-mode', mode.value)
  }

  function setMode(m: 'minimal' | 'full') {
    mode.value = m
    localStorage.setItem('sidebar-mode', mode.value)
  }

  function openMobile() {
    mobileOpen.value = true
  }
  function closeMobile() {
    mobileOpen.value = false
  }
  function toggleMobile() {
    mobileOpen.value = !mobileOpen.value
  }

  return {
    mode,
    mobileOpen,
    isMinimal,
    sidebarWidth,
    toggleMode,
    setMode,
    openMobile,
    closeMobile,
    toggleMobile,
  }
})
