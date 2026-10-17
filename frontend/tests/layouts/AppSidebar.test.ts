import { createMemoryHistory, createRouter } from 'vue-router'
import { createTestingPinia } from '@pinia/testing'
import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { Building, Home } from '@vicons/tabler'
import AppSidebar from '@/layouts/AppSidebar.vue'
import type { MenuMode, MenuTheme } from '@/lib/layout-preferences'
import i18n from '@/plugins/i18n'
import { storeIn } from '../helpers/pinia'

// Base ships no menu groups; one is needed here to see group titles.
vi.mock('@/layouts/menu-groups', () => ({
  MENU_GROUPS: [
    { id: 'workspace', labelKey: 'layout.palette.pages', icon: { template: '<svg />' }, order: 2 },
  ],
}))

const t = i18n.global.t
const stub = { template: '<div />' }

type Listener = (event: { matches: boolean }) => void

function stubBelowLg(initial: boolean) {
  const listeners = new Set<Listener>()
  const list = {
    matches: initial,
    addEventListener: (_: string, listener: Listener) => listeners.add(listener),
    removeEventListener: (_: string, listener: Listener) => listeners.delete(listener),
  }
  vi.stubGlobal(
    'matchMedia',
    vi.fn(() => list),
  )
  return {
    change(matches: boolean) {
      list.matches = matches
      listeners.forEach((listener) => listener({ matches }))
    },
  }
}

async function buildRouter() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      {
        path: '/',
        name: 'layout',
        component: stub,
        children: [
          {
            path: '',
            name: 'home',
            component: stub,
            meta: { sidebar: { labelKey: 'nav.home', icon: Home, order: 1 } },
          },
          {
            path: 'organizations',
            name: 'organizations',
            component: stub,
            meta: {
              sidebar: { labelKey: 'nav.organizations', icon: Building, group: 'workspace' },
            },
          },
        ],
      },
      { path: '/login', name: 'login', component: stub },
    ],
  })
  await router.push('/')
  await router.isReady()
  return router
}

async function mountSidebar(
  layout: {
    menuMode?: MenuMode
    menuTheme?: MenuTheme
    staticInactive?: boolean
    mobileOpen?: boolean
  } = {},
) {
  const router = await buildRouter()
  const pinia = createTestingPinia({
    createSpy: vi.fn,
    stubActions: false,
    initialState: { layout },
  })

  const wrapper = mount(AppSidebar, {
    global: { plugins: [pinia, router], stubs: { teleport: true } },
  })

  return { wrapper, router, layout: storeIn(pinia, useLayoutStore) }
}

describe('AppSidebar', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    useThemePreference().setPreference('light')
  })

  it.each(['static', 'slim', 'slim-plus'] as const)('renders the %s layout', async (mode) => {
    const { wrapper } = await mountSidebar({ menuMode: mode })

    const aside = wrapper.get('[data-testid="app-sidebar"]')
    expect(aside.attributes('data-mode')).toBe(mode)

    const groupTitle = t('layout.palette.pages')
    if (mode === 'static') {
      expect(aside.attributes('style')).toContain('width: 16rem')
      expect(aside.text()).toContain(groupTitle)
      expect(aside.text()).toContain(t('nav.home'))
      expect(aside.text()).toContain(t('nav.organizations'))
    } else if (mode === 'slim') {
      expect(aside.attributes('style')).toContain('width: 4rem')
      expect(aside.text()).not.toContain(t('nav.home'))
      expect(aside.text()).not.toContain(groupTitle)
      // The group is one rail entry, named for screen readers.
      expect(aside.find(`button[aria-label="${groupTitle}"]`).exists()).toBe(true)
    } else {
      expect(aside.attributes('style')).toContain('width: 6rem')
      expect(aside.text()).toContain(t('nav.home'))
      expect(aside.text()).toContain(groupTitle)
      // Group children live in the flyout, not on the rail.
      expect(aside.text()).not.toContain(t('nav.organizations'))
    }
  })

  it('renders the drawer mode as a rail and the horizontal mode as a full sidebar until they are built', async () => {
    const drawer = await mountSidebar({ menuMode: 'drawer' })
    const drawerAside = drawer.wrapper.get('[data-testid="app-sidebar"]')
    expect(drawerAside.attributes('data-mode')).toBe('drawer')
    expect(drawerAside.attributes('style')).toContain('width: 4rem')

    const horizontal = await mountSidebar({ menuMode: 'horizontal' })
    const horizontalAside = horizontal.wrapper.get('[data-testid="app-sidebar"]')
    expect(horizontalAside.attributes('data-mode')).toBe('horizontal')
    expect(horizontalAside.attributes('style')).toContain('width: 16rem')
  })

  it('hides the static sidebar when toggled off', async () => {
    const { wrapper, layout } = await mountSidebar({ menuMode: 'static', staticInactive: true })

    expect(wrapper.find('[data-testid="app-sidebar"]').exists()).toBe(false)

    // The mobile drawer still opens from the header toggle.
    layout.openMobile()
    await flushPromises()
    expect(wrapper.get('[data-testid="app-sidebar-mobile"]').text()).toContain(t('nav.home'))
  })

  it('keeps a rail visible when the static sidebar was toggled off in another mode', async () => {
    const { wrapper } = await mountSidebar({ menuMode: 'slim', staticInactive: true })

    expect(wrapper.find('[data-testid="app-sidebar"]').exists()).toBe(true)
  })

  it('paints the sidebar with the chosen menu color', async () => {
    const brand = await mountSidebar({ menuTheme: 'brand' })
    const brandAside = brand.wrapper.get('[data-testid="app-sidebar"]')
    expect(brandAside.attributes('data-theme')).toBe('brand')
    expect(brandAside.classes()).toContain('bg-primary-600')

    const transparent = await mountSidebar({ menuTheme: 'transparent' })
    const transparentAside = transparent.wrapper.get('[data-testid="app-sidebar"]')
    expect(transparentAside.attributes('data-theme')).toBe('transparent')
    expect(transparentAside.classes()).toContain('bg-surface-100')
  })

  it('uses the theme-aware wordmark only for transparent light', async () => {
    const wordmarkOf = (wrapper: Awaited<ReturnType<typeof mountSidebar>>['wrapper']) =>
      wrapper.get(`[data-testid="app-sidebar-home"] img`).attributes('src')

    const brand = await mountSidebar({ menuTheme: 'brand' })
    expect(wordmarkOf(brand.wrapper)).toBe('/weatherplus-wordmark-dark.webp')

    const transparent = await mountSidebar({ menuTheme: 'transparent' })
    expect(wordmarkOf(transparent.wrapper)).toBe('/weatherplus-wordmark-light.webp')

    useThemePreference().setPreference('dark')
    const transparentDark = await mountSidebar({ menuTheme: 'transparent' })
    expect(wordmarkOf(transparentDark.wrapper)).toBe('/weatherplus-wordmark-dark.webp')
  })

  it('shows the brand mark instead of the wordmark on a rail', async () => {
    const { wrapper } = await mountSidebar({ menuMode: 'slim' })

    expect(wrapper.get('[data-testid="app-sidebar-home"] img').attributes('src')).toBe(
      '/weatherplus-mark.webp',
    )
  })

  it('has no account or logout controls', async () => {
    const { wrapper } = await mountSidebar({ mobileOpen: true })
    await flushPromises()

    expect(wrapper.find('[data-testid="app-sidebar-mobile"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="app-sidebar-account"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="app-sidebar-logout"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="organization-switcher"]').exists()).toBe(false)
    expect(wrapper.find(`[aria-label="${t('auth.logout')}"]`).exists()).toBe(false)
    expect(wrapper.find(`[aria-label="${t('nav.profile')}"]`).exists()).toBe(false)
  })

  it('shows group titles in the mobile drawer', async () => {
    const { wrapper } = await mountSidebar({ menuMode: 'slim', mobileOpen: true })
    await flushPromises()

    const mobileNav = wrapper.get('[data-testid="app-sidebar-mobile"]')
    expect(mobileNav.text()).toContain(t('layout.palette.pages'))
    expect(mobileNav.text()).toContain(t('nav.organizations'))
  })

  it('navigates home and closes the mobile drawer when the logo button is clicked', async () => {
    const { wrapper, router, layout } = await mountSidebar()
    await router.push('/organizations')
    layout.openMobile()

    await wrapper.get('[data-testid="app-sidebar-home"]').trigger('click')
    await flushPromises()

    expect(layout.mobileOpen).toBe(false)
    expect(router.currentRoute.value.name).toBe('home')
  })

  it('closes the mobile drawer when the window grows past lg', async () => {
    const media = stubBelowLg(true)
    const { layout } = await mountSidebar()
    layout.openMobile()

    media.change(false)
    await flushPromises()

    expect(layout.mobileOpen).toBe(false)
  })
})
