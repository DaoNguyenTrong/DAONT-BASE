import { createMemoryHistory, createRouter } from 'vue-router'
import { createTestingPinia } from '@pinia/testing'
import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import AppHeader from '@/layouts/AppHeader.vue'
import { useSidebarStore } from '@/stores/sidebar-store'

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
            path: 'accounts',
            name: 'accounts',
            component: stub,
            meta: { breadcrumbKey: 'nav.accounts' },
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
    const { wrapper } = await mountHeader('/accounts')

    const items = wrapper.findAll('.n-breadcrumb-item')
    expect(items).toHaveLength(2)
    expect(items[1].text()).toContain('Accounts')
  })

  it('navigates to the clicked breadcrumb without a full page reload', async () => {
    const { wrapper, router } = await mountHeader('/accounts')

    const homeCrumb = wrapper.findAll('.n-breadcrumb-item')[0]
    await homeCrumb.find('a').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('home')
  })
})
