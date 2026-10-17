import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { defineComponent, onUnmounted } from 'vue'
import AppLayout from '@/layouts/AppLayout.vue'
import { BELOW_LG_QUERY } from '@/lib/responsive'
import { renderComponent } from '../helpers/render'
import { storeIn } from '../helpers/pinia'
import i18n from '@/plugins/i18n'

const t = i18n.global.t

const profileUnmounted = vi.fn()
const ProfileDialogStub = defineComponent({
  props: { visible: Boolean },
  setup() {
    onUnmounted(profileUnmounted)
  },
  template: '<div data-testid="profile-stub" :data-visible="visible" />',
})

// The real panel, palette and dialog are covered by their own tests; the palette also listens on `window`.
const overlayStubs = {
  AppearanceSettings: { template: '<div data-testid="settings-stub" />' },
  CommandPalette: { template: '<div data-testid="palette-stub" />' },
  ProfileDialog: ProfileDialogStub,
}

type Listener = (event: { matches: boolean }) => void

// Returns a setter that moves the viewport across `lg`, notifying every `useMediaQuery(BELOW_LG_QUERY)`.
function stubBelowLg(initial: boolean) {
  let matches = initial
  const listeners = new Set<Listener>()
  vi.stubGlobal(
    'matchMedia',
    vi.fn((query: string) => ({
      get matches() {
        return query === BELOW_LG_QUERY ? matches : false
      },
      addEventListener: (_: string, listener: Listener) => {
        if (query === BELOW_LG_QUERY) listeners.add(listener)
      },
      removeEventListener: (_: string, listener: Listener) => listeners.delete(listener),
    })),
  )
  return (next: boolean) => {
    matches = next
    listeners.forEach((listener) => listener({ matches: next }))
  }
}

// The real sidebar stays mounted (it owns the mobile drawer), so "no sidebar" is checked against it.
const horizontalStubs = {
  ...overlayStubs,
  AppTopMenu: { template: '<div data-testid="app-top-menu" />' },
  AppHeader: { template: '<div data-testid="header-stub" />' },
  AppFooter: true,
  teleport: true,
}

async function renderHorizontal() {
  const rendered = await renderComponent(AppLayout, {
    initialState: { layout: { menuMode: 'horizontal' } },
    global: { stubs: horizontalStubs },
  })
  // The page title comes from the deepest route that declares a breadcrumb.
  rendered.router.addRoute({
    path: '/reports',
    name: 'reports',
    component: { template: '<div data-testid="page" />' },
    meta: { breadcrumbKey: 'nav.organizations' },
  })
  await rendered.router.push('/reports')
  await flushPromises()
  return rendered
}

describe('AppLayout', () => {
  // Cleared before, not after: the previous test's wrapper is unmounted by tests/setup.ts after this
  // file's own afterEach hooks have run.
  beforeEach(() => {
    profileUnmounted.mockClear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('renders the sidebar, header, footer, and the matched route view', async () => {
    const { wrapper } = await renderComponent(AppLayout, {
      global: {
        stubs: {
          ...overlayStubs,
          AppSidebar: { template: '<div class="sidebar-stub" />' },
          AppHeader: { template: '<div class="header-stub" />' },
          AppFooter: { template: '<div class="footer-stub" />' },
        },
      },
    })

    expect(wrapper.find('[data-testid="app-shell"]').exists()).toBe(true)
    expect(wrapper.find('.sidebar-stub').exists()).toBe(true)
    expect(wrapper.find('.header-stub').exists()).toBe(true)
    expect(wrapper.find('.footer-stub').exists()).toBe(true)
    expect(wrapper.find('main').exists()).toBe(true)
  })

  it('renders the header before the routed content and the footer after it', async () => {
    const { wrapper } = await renderComponent(AppLayout, {
      global: {
        stubs: {
          ...overlayStubs,
          AppSidebar: { template: '<div />' },
          AppHeader: { template: '<div class="marker-header" />' },
          AppFooter: { template: '<div class="marker-footer" />' },
        },
      },
    })

    const html = wrapper.html()
    expect(html.indexOf('marker-header')).toBeLessThan(html.indexOf('<main'))
    expect(html.indexOf('<main')).toBeLessThan(html.indexOf('marker-footer'))
  })

  it('mounts the settings panel, the palette and the profile dialog once', async () => {
    const { wrapper, pinia } = await renderComponent(AppLayout, {
      global: {
        stubs: { ...overlayStubs, AppSidebar: true, AppHeader: true, AppFooter: true },
      },
    })

    expect(wrapper.findAll('[data-testid="settings-stub"]')).toHaveLength(1)
    expect(wrapper.findAll('[data-testid="palette-stub"]')).toHaveLength(1)
    expect(wrapper.findAll('[data-testid="profile-stub"]')).toHaveLength(1)

    storeIn(pinia, useLayoutStore).openProfile()
    await flushPromises()
    expect(wrapper.get('[data-testid="profile-stub"]').attributes('data-visible')).toBe('true')
  })

  it('keeps the open profile dialog mounted when horizontal mode crosses lg', async () => {
    const setBelowLg = stubBelowLg(false)
    const { wrapper, pinia } = await renderHorizontal()
    storeIn(pinia, useLayoutStore).openProfile()
    await flushPromises()

    // The top bar gives way to the header (and back), which unmounts the avatar menu inside it.
    setBelowLg(true)
    await flushPromises()
    expect(wrapper.find('[data-testid="header-stub"]').exists()).toBe(true)
    setBelowLg(false)
    await flushPromises()
    expect(wrapper.find('[data-testid="app-top-menu"]').exists()).toBe(true)

    expect(profileUnmounted).not.toHaveBeenCalled()
    expect(wrapper.findAll('[data-testid="profile-stub"]')).toHaveLength(1)
    expect(wrapper.get('[data-testid="profile-stub"]').attributes('data-visible')).toBe('true')
  })

  it('scrolls the main area back to the top on a path change, not on a query change', async () => {
    const { wrapper, router } = await renderComponent(AppLayout, {
      global: {
        stubs: { ...overlayStubs, AppSidebar: true, AppHeader: true, AppFooter: true },
      },
    })
    const main = wrapper.find('main').element as HTMLElement
    const scrollTo = vi.fn()
    main.scrollTo = scrollTo as unknown as HTMLElement['scrollTo']

    await router.push('/?page=2')
    await flushPromises()
    expect(scrollTo).not.toHaveBeenCalled()

    await router.push('/organizations')
    await flushPromises()
    expect(scrollTo).toHaveBeenCalledWith({ top: 0 })
  })

  it('starts the notification polling and realtime connection, and stops them on unmount', async () => {
    const { wrapper, pinia } = await renderComponent(AppLayout, {
      global: {
        stubs: { ...overlayStubs, AppSidebar: true, AppHeader: true, AppFooter: true },
      },
    })
    const notifications = storeIn(pinia, useNotificationsStore)

    expect(notifications.startPolling).toHaveBeenCalledTimes(1)
    expect(notifications.connectRealtime).toHaveBeenCalledTimes(1)

    wrapper.unmount()

    expect(notifications.stopPolling).toHaveBeenCalledTimes(1)
    expect(notifications.disconnectRealtime).toHaveBeenCalledTimes(1)
  })

  it('lays out the top menu and page title in horizontal mode on desktop', async () => {
    stubBelowLg(false)
    const { wrapper } = await renderHorizontal()

    expect(wrapper.find('[data-testid="app-sidebar"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="header-stub"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="app-top-menu"]').exists()).toBe(true)

    const main = wrapper.get('main')
    expect(main.get('[data-testid="app-header-title"]').text()).toBe(t('nav.organizations'))
    // The title row comes before the page.
    expect(main.html().indexOf('app-header-title')).toBeLessThan(
      main.html().indexOf('data-testid="page"'),
    )
  })

  it('keeps the header bar and the mobile drawer below lg in horizontal mode', async () => {
    stubBelowLg(true)
    const { wrapper, pinia } = await renderHorizontal()

    expect(wrapper.find('[data-testid="app-top-menu"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="header-stub"]').exists()).toBe(true)
    expect(wrapper.get('main').find('[data-testid="app-header-title"]').exists()).toBe(false)

    storeIn(pinia, useLayoutStore).openMobile()
    await flushPromises()
    expect(wrapper.find('[data-testid="app-sidebar-mobile"]').exists()).toBe(true)
  })
})
