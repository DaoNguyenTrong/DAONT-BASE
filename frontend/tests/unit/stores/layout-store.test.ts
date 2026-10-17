import { beforeEach, describe, expect, it } from 'vitest'
import { LAYOUT_STORAGE_KEYS, MENU_MODES } from '@/lib/layout-preferences'
import { useLayoutStore } from '@/stores/layout-store'
import { setupTestPinia } from '../../helpers/pinia'

describe('useLayoutStore', () => {
  beforeEach(() => {
    localStorage.clear()
    setupTestPinia()
  })

  it('restores persisted preferences at creation', () => {
    localStorage.setItem(LAYOUT_STORAGE_KEYS.menuMode, 'slim-plus')
    localStorage.setItem(LAYOUT_STORAGE_KEYS.menuTheme, 'transparent')
    localStorage.setItem(LAYOUT_STORAGE_KEYS.drawerPinned, 'true')
    localStorage.setItem(LAYOUT_STORAGE_KEYS.staticInactive, 'true')

    const layout = useLayoutStore()

    expect(layout.menuMode).toBe('slim-plus')
    expect(layout.menuTheme).toBe('transparent')
    expect(layout.drawerPinned).toBe(true)
    expect(layout.staticInactive).toBe(true)
  })

  it('starts with session state closed', () => {
    const layout = useLayoutStore()

    expect(layout.mobileOpen).toBe(false)
    expect(layout.drawerExpanded).toBe(false)
    expect(layout.settingsOpen).toBe(false)
    expect(layout.paletteOpen).toBe(false)
    expect(layout.profileOpen).toBe(false)
  })

  it('setMenuMode persists and clears session state', () => {
    const layout = useLayoutStore()
    layout.expandDrawer()
    layout.openMobile()

    layout.setMenuMode('horizontal')

    expect(layout.menuMode).toBe('horizontal')
    expect(localStorage.getItem(LAYOUT_STORAGE_KEYS.menuMode)).toBe('horizontal')
    expect(layout.drawerExpanded).toBe(false)
    expect(layout.mobileOpen).toBe(false)
  })

  it('setMenuTheme persists the theme', () => {
    const layout = useLayoutStore()

    layout.setMenuTheme('transparent')

    expect(layout.menuTheme).toBe('transparent')
    expect(localStorage.getItem(LAYOUT_STORAGE_KEYS.menuTheme)).toBe('transparent')
  })

  it.each(MENU_MODES.flatMap((mode) => [true, false].map((pinned) => [mode, pinned] as const)))(
    'isRail for %s with drawerPinned=%s matches the rule',
    (mode, pinned) => {
      const layout = useLayoutStore()
      layout.setMenuMode(mode)
      layout.setDrawerPinned(pinned)

      const expected = mode === 'slim' || mode === 'slim-plus' || (mode === 'drawer' && !pinned)
      expect(layout.isRail).toBe(expected)
    },
  )

  it('railWidth is 6rem for slim-plus and 4rem otherwise', () => {
    const layout = useLayoutStore()

    for (const mode of MENU_MODES) {
      layout.setMenuMode(mode)
      expect(layout.railWidth).toBe(mode === 'slim-plus' ? '6rem' : '4rem')
    }
  })

  it('toggleStaticSidebar flips and persists staticInactive', () => {
    const layout = useLayoutStore()

    layout.toggleStaticSidebar()
    expect(layout.staticInactive).toBe(true)
    expect(localStorage.getItem(LAYOUT_STORAGE_KEYS.staticInactive)).toBe('true')

    layout.toggleStaticSidebar()
    expect(layout.staticInactive).toBe(false)
    expect(localStorage.getItem(LAYOUT_STORAGE_KEYS.staticInactive)).toBe('false')
  })

  it('setDrawerPinned persists and collapses the expanded drawer', () => {
    const layout = useLayoutStore()
    layout.expandDrawer()

    layout.setDrawerPinned(true)

    expect(layout.drawerPinned).toBe(true)
    expect(localStorage.getItem(LAYOUT_STORAGE_KEYS.drawerPinned)).toBe('true')
    expect(layout.drawerExpanded).toBe(false)
  })

  it('opens and closes the session-only surfaces', () => {
    const layout = useLayoutStore()

    layout.openMobile()
    layout.openSettings()
    layout.openPalette()
    layout.openProfile()
    layout.expandDrawer()
    expect([
      layout.mobileOpen,
      layout.settingsOpen,
      layout.paletteOpen,
      layout.profileOpen,
      layout.drawerExpanded,
    ]).toEqual([true, true, true, true, true])

    layout.closeMobile()
    layout.closeSettings()
    layout.closePalette()
    layout.closeProfile()
    layout.collapseDrawer()
    expect([
      layout.mobileOpen,
      layout.settingsOpen,
      layout.paletteOpen,
      layout.profileOpen,
      layout.drawerExpanded,
    ]).toEqual([false, false, false, false, false])
  })
})
