import { describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import AppLayout from '@/layouts/AppLayout.vue'
import { renderComponent } from '../helpers/render'

describe('AppLayout', () => {
  it('renders the sidebar, header, footer, and the matched route view', async () => {
    const { wrapper } = await renderComponent(AppLayout, {
      global: {
        stubs: {
          AppSidebar: { template: '<div class="sidebar-stub" />' },
          AppHeader: { template: '<div class="header-stub" />' },
          AppFooter: { template: '<div class="footer-stub" />' },
        },
      },
    })

    expect(wrapper.find('.sidebar-stub').exists()).toBe(true)
    expect(wrapper.find('.header-stub').exists()).toBe(true)
    expect(wrapper.find('.footer-stub').exists()).toBe(true)
    expect(wrapper.find('main').exists()).toBe(true)
  })

  it('renders the header before the routed content and the footer after it', async () => {
    const { wrapper } = await renderComponent(AppLayout, {
      global: {
        stubs: {
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

  it('scrolls the main area back to the top on a path change, not on a query change', async () => {
    const { wrapper, router } = await renderComponent(AppLayout, {
      global: {
        stubs: { AppSidebar: true, AppHeader: true, AppFooter: true },
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
})
