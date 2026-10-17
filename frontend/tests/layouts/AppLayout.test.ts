import { describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import AppLayout from '@/layouts/AppLayout.vue'
import { renderComponent } from '../helpers/render'
import { storeIn } from '../helpers/pinia'

// The real panel and palette are covered by their own tests; the palette also listens on `window`.
const overlayStubs = {
  AppearanceSettings: { template: '<div data-testid="settings-stub" />' },
  CommandPalette: { template: '<div data-testid="palette-stub" />' },
}

describe('AppLayout', () => {
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

  it('mounts the settings panel and the palette once', async () => {
    const { wrapper } = await renderComponent(AppLayout, {
      global: {
        stubs: { ...overlayStubs, AppSidebar: true, AppHeader: true, AppFooter: true },
      },
    })

    expect(wrapper.findAll('[data-testid="settings-stub"]')).toHaveLength(1)
    expect(wrapper.findAll('[data-testid="palette-stub"]')).toHaveLength(1)
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
})
