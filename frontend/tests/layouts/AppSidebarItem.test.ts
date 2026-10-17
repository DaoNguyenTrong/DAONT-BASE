import { createMemoryHistory, createRouter } from 'vue-router'
import { createTestingPinia } from '@pinia/testing'
import { flushPromises, mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import AppSidebarItem from '@/layouts/AppSidebarItem.vue'
import type { SidebarMenuGroup, SidebarMenuItem } from '@/layouts/sidebar-menu'
import i18n from '@/plugins/i18n'
import { storeIn } from '../helpers/pinia'

const t = i18n.global.t
const stub = { template: '<div />' }
const iconStub = { template: '<svg />' }

async function buildRouter(initialPath: string) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'home', component: stub },
      { path: '/organizations', name: 'organizations', component: stub },
      { path: '/settings', name: 'settings', component: stub },
    ],
  })
  await router.push(initialPath)
  await router.isReady()
  return router
}

async function mountItem(
  item: SidebarMenuItem,
  props: Record<string, unknown> = {},
  initialPath = '/organizations',
) {
  const router = await buildRouter(initialPath)
  const pinia = createTestingPinia({ createSpy: vi.fn, stubActions: false })

  const wrapper = mount(AppSidebarItem, {
    props: { item, depth: 0, ...props },
    global: { plugins: [pinia, router], stubs: { teleport: true } },
  })

  return { wrapper, router, layout: storeIn(pinia, useLayoutStore) }
}

const parent: SidebarMenuItem = {
  labelKey: 'nav.profile',
  icon: iconStub,
  items: [
    { labelKey: 'nav.home', icon: iconStub, routeName: 'home' },
    { labelKey: 'nav.organizations', icon: iconStub, routeName: 'settings' },
  ],
}

const group: SidebarMenuGroup = {
  kind: 'group',
  id: 'workspace',
  labelKey: 'layout.palette.pages',
  icon: iconStub,
  items: [
    { labelKey: 'nav.home', icon: iconStub, routeName: 'home' },
    { labelKey: 'nav.organizations', icon: iconStub, routeName: 'organizations' },
  ],
}

describe('AppSidebarItem', () => {
  it('renders a leaf item as a router link and highlights it when active', async () => {
    const { wrapper } = await mountItem({
      labelKey: 'nav.organizations',
      icon: iconStub,
      routeName: 'organizations',
    })

    const link = wrapper.get('a')
    expect(link.text()).toContain(t('nav.organizations'))
    expect(link.attributes('aria-current')).toBe('page')
    expect(link.classes()).toContain('bg-white/15')
  })

  it('does not highlight a leaf item that does not match the current route', async () => {
    const { wrapper } = await mountItem({ labelKey: 'nav.home', icon: iconStub, routeName: 'home' })

    expect(wrapper.get('a').attributes('aria-current')).toBeUndefined()
    expect(wrapper.get('a').classes()).not.toContain('bg-white/15')
  })

  it('closes the mobile drawer when a leaf item link is clicked', async () => {
    const { wrapper, layout } = await mountItem({
      labelKey: 'nav.home',
      icon: iconStub,
      routeName: 'home',
    })
    layout.openMobile()

    await wrapper.get('a').trigger('click')

    expect(layout.mobileOpen).toBe(false)
  })

  it('expands and collapses a parent item with children on click', async () => {
    const { wrapper } = await mountItem(parent)

    const toggle = wrapper.get('button')
    expect(toggle.attributes('aria-expanded')).toBe('false')
    expect(wrapper.findAll('a')).toHaveLength(0)

    await toggle.trigger('click')
    expect(toggle.attributes('aria-expanded')).toBe('true')
    expect(wrapper.findAll('a')).toHaveLength(2)

    await toggle.trigger('click')
    expect(wrapper.findAll('a')).toHaveLength(0)
  })

  it('opens the accordion of the parent that contains the current route', async () => {
    const { wrapper } = await mountItem(parent, {}, '/settings')

    const toggle = wrapper.get('button')
    expect(toggle.attributes('aria-expanded')).toBe('true')
    expect(toggle.classes()).toContain('font-semibold')
    const active = wrapper.get('a[aria-current="page"]')
    expect(active.text()).toContain(t('nav.organizations'))
  })

  it('opens the accordion when the route moves into it later', async () => {
    const { wrapper, router } = await mountItem(parent, {}, '/organizations')
    expect(wrapper.get('button').attributes('aria-expanded')).toBe('false')

    await router.push('/settings')
    await flushPromises()

    expect(wrapper.get('button').attributes('aria-expanded')).toBe('true')
  })

  it('renders a slim rail leaf as an icon-only link', async () => {
    const { wrapper } = await mountItem(
      { labelKey: 'nav.organizations', icon: iconStub, routeName: 'organizations' },
      { variant: 'slim' },
    )

    const link = wrapper.get('a')
    expect(link.attributes('aria-label')).toBe(t('nav.organizations'))
    expect(link.text()).not.toContain(t('nav.organizations'))
    expect(link.classes()).toContain('bg-white/15')
  })

  it('renders a slim-plus rail leaf with its label under the icon and the full label as a title', async () => {
    const { wrapper } = await mountItem(
      { labelKey: 'nav.organizations', icon: iconStub, routeName: 'organizations' },
      { variant: 'slim-plus' },
    )

    const link = wrapper.get('a')
    expect(link.text()).toContain(t('nav.organizations'))
    expect(link.attributes('title')).toBe(t('nav.organizations'))
    expect(link.find('.line-clamp-2').exists()).toBe(true)
  })

  it('opens a group flyout from the rail and marks the current page', async () => {
    const { wrapper } = await mountItem(group, { variant: 'slim' })

    const trigger = wrapper.get(`button[aria-label="${t('layout.palette.pages')}"]`)
    // The rail entry holding the current page is highlighted too.
    expect(trigger.classes()).toContain('bg-white/15')

    await trigger.trigger('click')
    await flushPromises()

    const menu = wrapper.get('.n-dropdown-menu')
    expect(menu.get('.n-dropdown-option-body--group').text()).toContain(t('layout.palette.pages'))
    expect(menu.text()).toContain(t('nav.home'))
    expect(menu.get('.n-dropdown-option-body--active').text()).toContain(t('nav.organizations'))
  })

  it('navigates to the page picked in the flyout', async () => {
    const { wrapper, router } = await mountItem(group, { variant: 'slim-plus' })

    await wrapper.get(`button[aria-label="${t('layout.palette.pages')}"]`).trigger('click')
    await flushPromises()
    const home = wrapper
      .findAll('.n-dropdown-option-body')
      .find((option) => option.text().includes(t('nav.home')))!
    await home.trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('home')
  })
})
