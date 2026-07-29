import { createMemoryHistory, createRouter } from 'vue-router'
import { createTestingPinia } from '@pinia/testing'
import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import AppSidebarItem from '@/layouts/AppSidebarItem.vue'
import type { SidebarMenuItem } from '@/layouts/sidebar-menu'
import { useSidebarStore } from '@/stores/sidebar-store'

const stub = { template: '<div />' }
const iconStub = { template: '<svg />' }

async function buildRouter(initialPath = '/accounts') {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'home', component: stub },
      { path: '/accounts', name: 'accounts', component: stub },
      { path: '/settings', name: 'settings', component: stub },
    ],
  })
  await router.push(initialPath)
  await router.isReady()
  return router
}

async function mountItem(item: SidebarMenuItem, props: Record<string, unknown> = {}) {
  const router = await buildRouter()
  const pinia = createTestingPinia({ createSpy: vi.fn, stubActions: false })

  const wrapper = mount(AppSidebarItem, {
    props: { item, depth: 0, ...props },
    global: { plugins: [pinia, router], stubs: { teleport: true } },
  })

  return { wrapper, router }
}

describe('AppSidebarItem', () => {
  it('renders a leaf item as a router link and highlights it when active', async () => {
    const item: SidebarMenuItem = { labelKey: 'nav.accounts', icon: iconStub, routeName: 'accounts' }
    const { wrapper } = await mountItem(item)

    const link = wrapper.get('a')
    expect(link.text()).toContain('Accounts')
    expect(link.classes()).toContain('bg-white/15')
  })

  it('does not highlight a leaf item that does not match the current route', async () => {
    const item: SidebarMenuItem = { labelKey: 'nav.home', icon: iconStub, routeName: 'home' }
    const { wrapper } = await mountItem(item)

    expect(wrapper.get('a').classes()).not.toContain('bg-white/15')
  })

  it('closes the mobile drawer when a leaf item link is clicked', async () => {
    const item: SidebarMenuItem = { labelKey: 'nav.home', icon: iconStub, routeName: 'home' }
    const { wrapper } = await mountItem(item)
    const sidebar = useSidebarStore()
    sidebar.openMobile()

    await wrapper.get('a').trigger('click')

    expect(sidebar.mobileOpen).toBe(false)
  })

  it('expands and collapses a parent item with children on click', async () => {
    const item: SidebarMenuItem = {
      labelKey: 'nav.profile',
      icon: iconStub,
      items: [{ labelKey: 'nav.accounts', icon: iconStub, routeName: 'settings' }],
    }
    const { wrapper } = await mountItem(item)

    expect(wrapper.findAllComponents(AppSidebarItem)).toHaveLength(0)

    await wrapper.get('button').trigger('click')
    expect(wrapper.findAllComponents(AppSidebarItem)).toHaveLength(1)

    await wrapper.get('button').trigger('click')
    expect(wrapper.findAllComponents(AppSidebarItem)).toHaveLength(0)
  })

  it('renders a minimal-mode leaf item as an icon-only tooltip link', async () => {
    const item: SidebarMenuItem = { labelKey: 'nav.accounts', icon: iconStub, routeName: 'accounts' }
    const { wrapper } = await mountItem(item, { minimal: true })

    expect(wrapper.find('a').exists()).toBe(true)
    expect(wrapper.text()).not.toContain('Accounts')
  })

  it('renders a minimal-mode parent item as a dropdown trigger button', async () => {
    const item: SidebarMenuItem = {
      labelKey: 'nav.profile',
      icon: iconStub,
      items: [{ labelKey: 'nav.accounts', icon: iconStub, routeName: 'settings' }],
    }
    const { wrapper } = await mountItem(item, { minimal: true })

    expect(wrapper.find('a').exists()).toBe(false)
    expect(wrapper.get('button').attributes('aria-label')).toBe('Profile')
  })
})
