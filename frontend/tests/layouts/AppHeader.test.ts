import { createMemoryHistory, createRouter } from 'vue-router'
import { createTestingPinia } from '@pinia/testing'
import { mount } from '@vue/test-utils'
import { NButton } from 'naive-ui'
import { afterEach, describe, expect, it, vi } from 'vitest'
import AppHeader from '@/layouts/AppHeader.vue'
import type { MenuMode } from '@/lib/layout-preferences'
import en from '@/locales/en'
import i18n from '@/plugins/i18n'
import { storeIn } from '../helpers/pinia'

const stub = { template: '<div />' }
const t = i18n.global.t

function stubBelowLg(matches: boolean) {
  vi.stubGlobal(
    'matchMedia',
    vi.fn(() => ({ matches, addEventListener: vi.fn(), removeEventListener: vi.fn() })),
  )
}

async function mountHeader(
  options: {
    path?: string
    variant?: 'bar' | 'actions'
    tone?: 'default' | 'on-brand'
    menuMode?: MenuMode
  } = {},
) {
  const { path = '/', variant, tone, menuMode } = options
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      {
        path: '/',
        name: 'layout',
        component: stub,
        children: [
          { path: '', name: 'home', component: stub, meta: { breadcrumbKey: 'nav.home' } },
          {
            path: 'organizations',
            name: 'organizations',
            component: stub,
            meta: { breadcrumbKey: 'nav.organizations' },
          },
        ],
      },
    ],
  })
  await router.push(path)
  await router.isReady()
  const pinia = createTestingPinia({
    createSpy: vi.fn,
    stubActions: false,
    initialState: menuMode ? { layout: { menuMode } } : {},
  })

  const wrapper = mount(AppHeader, {
    props: { ...(variant ? { variant } : {}), ...(tone ? { tone } : {}) },
    global: {
      plugins: [pinia, router],
      stubs: {
        UserMenu: {
          props: ['tone'],
          template: '<div data-testid="user-menu-stub" :data-tone="tone" />',
        },
        NotificationBell: {
          props: ['tone'],
          template: '<div data-testid="bell-stub" :data-tone="tone" />',
        },
      },
    },
  })

  return { wrapper, layout: storeIn(pinia, useLayoutStore) }
}

describe('AppHeader', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    localStorage.clear()
  })

  it('shows the page title and its crumbs', async () => {
    const { wrapper } = await mountHeader({ path: '/organizations' })

    expect(wrapper.get('[data-testid="app-header-title"]').text()).toBe(en.nav.organizations)
    expect(wrapper.findAll('.n-breadcrumb-item')).toHaveLength(1)
  })

  it.each(['slim', 'slim-plus', 'drawer', 'horizontal'] as const)(
    'hides the menu toggle on desktop except in static mode (%s)',
    async (menuMode) => {
      const { wrapper } = await mountHeader({ menuMode })

      expect(wrapper.find('[data-testid="app-header-toggle"]').exists()).toBe(false)
    },
  )

  it('toggles the static sidebar on desktop', async () => {
    const { wrapper, layout } = await mountHeader()

    await wrapper.get('[data-testid="app-header-toggle"]').trigger('click')
    expect(layout.staticInactive).toBe(true)
    await wrapper.get('[data-testid="app-header-toggle"]').trigger('click')
    expect(layout.staticInactive).toBe(false)
    expect(layout.mobileOpen).toBe(false)
  })

  it('opens the mobile drawer below lg in any mode', async () => {
    stubBelowLg(true)
    const { wrapper, layout } = await mountHeader({ menuMode: 'slim' })

    await wrapper.get('[data-testid="app-header-toggle"]').trigger('click')

    expect(layout.mobileOpen).toBe(true)
    expect(layout.staticInactive).toBe(false)
  })

  it('opens the palette and the settings panel', async () => {
    const { wrapper, layout } = await mountHeader()

    await wrapper.get('[data-testid="app-header-palette"]').trigger('click')
    await wrapper.get('[data-testid="app-header-settings"]').trigger('click')

    expect(layout.paletteOpen).toBe(true)
    expect(layout.settingsOpen).toBe(true)
    expect(wrapper.get('[data-testid="app-header-settings"]').attributes('aria-label')).toBe(
      t('layout.openSettings'),
    )
  })

  it('renders only the action cluster in the actions variant', async () => {
    const { wrapper } = await mountHeader({ variant: 'actions' })

    expect(wrapper.find('[data-testid="app-header-toggle"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="app-header-title"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="app-header-palette"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="app-header-settings"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="app-header-user-guide"]').exists()).toBe(true)
  })

  it('draws the action cluster for the brand menu bar when on brand', async () => {
    const brand = await mountHeader({ variant: 'actions', tone: 'on-brand' })

    const pill = brand.wrapper.get('[data-testid="app-header-palette"]')
    expect(pill.classes()).toEqual(expect.arrayContaining(['border-white/20', 'bg-white/10']))
    expect(pill.classes()).toContain('text-white/80')
    expect(pill.classes()).not.toContain('text-surface-500')
    for (const testid of ['app-header-user-guide', 'app-header-settings']) {
      const button = brand.wrapper
        .findAllComponents(NButton)
        .find((b) => b.attributes('data-testid') === testid)!
      expect(button.props('textColor')).toBeTruthy()
    }
    expect(brand.wrapper.get('[data-testid="user-menu-stub"]').attributes('data-tone')).toBe(
      'on-brand',
    )
    expect(brand.wrapper.get('[data-testid="bell-stub"]').attributes('data-tone')).toBe('on-brand')
    brand.wrapper.unmount()

    const plain = await mountHeader({ variant: 'actions' })
    const plainPill = plain.wrapper.get('[data-testid="app-header-palette"]')
    expect(plainPill.classes()).toContain('text-surface-500')
    expect(plainPill.classes()).not.toContain('bg-white/10')
    for (const button of plain.wrapper.findAllComponents(NButton)) {
      expect(button.props('textColor')).toBeUndefined()
    }
    expect(plain.wrapper.get('[data-testid="user-menu-stub"]').attributes('data-tone')).toBe(
      'default',
    )
  })

  it('links to the user guide in a new tab', async () => {
    const { wrapper } = await mountHeader()

    const link = wrapper.get('[data-testid="app-header-user-guide"]')
    expect(link.element.tagName).toBe('A')
    expect(link.attributes('href')).toBe('/guide/en/')
    expect(link.attributes('target')).toBe('_blank')
    expect(link.attributes('rel')).toContain('noopener')
    expect(link.attributes('aria-label')).toBe(en.nav.userGuide)
  })

  it('opens the Vietnamese guide when the app is in Vietnamese', async () => {
    i18n.global.locale.value = 'vi'
    try {
      const { wrapper } = await mountHeader()

      expect(wrapper.get('[data-testid="app-header-user-guide"]').attributes('href')).toBe(
        '/guide/',
      )
    } finally {
      i18n.global.locale.value = 'en'
    }
  })
})
