import { createMemoryHistory, createRouter } from 'vue-router'
import { createTestingPinia } from '@pinia/testing'
import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import AppHeader from '@/layouts/AppHeader.vue'
import { useSidebarStore } from '@/stores/sidebar-store'
import en from '@/locales/en'
import i18n from '@/plugins/i18n'

const stub = { template: '<div />' }

function buildRouter(initialPath = '/') {
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
            meta: { breadcrumbKey: 'nav.home' },
          },
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
  return router
    .push(initialPath)
    .then(() => router.isReady())
    .then(() => router)
}

async function mountHeader(initialPath = '/') {
  const router = await buildRouter(initialPath)
  const pinia = createTestingPinia({ createSpy: vi.fn, stubActions: false })

  const wrapper = mount(AppHeader, {
    global: { plugins: [pinia, router], stubs: { AppControls: true } },
  })

  return { wrapper, router }
}

describe('AppHeader', () => {
  const originalWidth = window.innerWidth

  afterEach(() => {
    Object.defineProperty(window, 'innerWidth', { value: originalWidth, configurable: true })
  })

  it('opens the mobile drawer when toggled below the desktop breakpoint', async () => {
    Object.defineProperty(window, 'innerWidth', { value: 800, configurable: true })
    const { wrapper } = await mountHeader()
    const sidebar = useSidebarStore()

    await wrapper.find('button').trigger('click')

    expect(sidebar.mobileOpen).toBe(true)
    expect(sidebar.mode).toBe('full')
  })

  it('toggles the sidebar mode when toggled at or above the desktop breakpoint', async () => {
    Object.defineProperty(window, 'innerWidth', { value: 1280, configurable: true })
    const { wrapper } = await mountHeader()
    const sidebar = useSidebarStore()

    await wrapper.find('button').trigger('click')

    expect(sidebar.mode).toBe('minimal')
    expect(sidebar.mobileOpen).toBe(false)
  })

  it('shows a home breadcrumb plus one entry per matched route with a breadcrumbKey', async () => {
    const { wrapper } = await mountHeader('/organizations')

    const items = wrapper.findAll('.n-breadcrumb-item')
    expect(items).toHaveLength(2)
    expect(items[1]!.text()).toContain(en.nav.organizations)
  })

  // The crumb is cut with an ellipsis when it is long, so its full text must stay reachable on hover.
  it('keeps the full crumb label in a title, for when a long one is truncated', async () => {
    const { wrapper } = await mountHeader('/organizations')

    const crumb = wrapper.get('[data-testid="app-header-crumb-text"]')
    expect(crumb.attributes('title')).toBe(en.nav.organizations)
  })

  it('navigates to the clicked breadcrumb without a full page reload', async () => {
    const { wrapper, router } = await mountHeader('/organizations')

    const homeCrumb = wrapper.findAll('.n-breadcrumb-item')[0]!
    await homeCrumb.find('a').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('home')
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
