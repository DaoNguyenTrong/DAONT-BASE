import { flushPromises } from '@vue/test-utils'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import AppFooter from '@/layouts/AppFooter.vue'
import { server } from '../helpers/msw/server'
import { renderComponent } from '../helpers/render'

function mockHealth(overrides: Record<string, unknown> = {}) {
  server.use(
    http.get('*/api/health', () =>
      HttpResponse.json({
        status: 'healthy',
        version: '1.2.0',
        timestamp: '2026-06-01T10:00:00Z',
        buildTimestamp: null,
        ...overrides,
      }),
    ),
  )
}

describe('AppFooter', () => {
  it('renders the app name and version', async () => {
    const { wrapper } = await renderComponent(AppFooter)

    expect(wrapper.text()).toContain('App Starter')
    expect(wrapper.text()).toMatch(/Version \d+\.\d+\.\d+/)
  })

  it('shows the offline status dot while the health check is pending', async () => {
    server.use(http.get('*/api/health', () => new Promise<Response>(() => {})))

    const { wrapper } = await renderComponent(AppFooter)

    expect(wrapper.find('[role="status"]').classes()).not.toContain('bg-green-500')
    expect(wrapper.find('[role="status"]').classes()).not.toContain('bg-red-500')
  })

  it('shows the online status dot and API version once the health check resolves healthy', async () => {
    mockHealth({ version: '3.1.4' })

    const { wrapper } = await renderComponent(AppFooter)
    await flushPromises()

    expect(wrapper.find('[role="status"]').classes()).toContain('bg-green-500')
    expect(wrapper.text()).toContain('API v3.1.4')
  })

  it('shows the offline status dot when the health check reports unhealthy', async () => {
    mockHealth({ status: 'degraded' })

    const { wrapper } = await renderComponent(AppFooter)
    await flushPromises()

    expect(wrapper.find('[role="status"]').classes()).toContain('bg-red-500')
  })

  it('formats and shows the API build time when the health check returns one', async () => {
    mockHealth({ buildTimestamp: '2026-02-02T00:00:00Z' })

    const { wrapper } = await renderComponent(AppFooter)
    await flushPromises()

    expect(wrapper.text()).toMatch(/Build .*2026/)
  })

  it('omits the build-time text when the health check returns no build timestamp', async () => {
    mockHealth({ buildTimestamp: null })

    const { wrapper } = await renderComponent(AppFooter)
    await flushPromises()

    expect(wrapper.text()).not.toContain('Build ')
  })
})
