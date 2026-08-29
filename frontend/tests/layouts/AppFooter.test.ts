import { describe, expect, it } from 'vitest'
import AppFooter from '@/layouts/AppFooter.vue'
import { renderComponent } from '../helpers/render'

// The footer only reflects the shared health store — polling is owned by App.vue —
// so these tests seed the store state rather than mock the /api/health request.

describe('AppFooter', () => {
  it('renders the app name and version', async () => {
    const { wrapper } = await renderComponent(AppFooter)

    expect(wrapper.text()).toContain('App Starter')
    expect(wrapper.text()).toMatch(/Version v?\d+\.\d+\.\d+/)
  })

  it('shows a neutral status dot while the health check is still "checking"', async () => {
    const { wrapper } = await renderComponent(AppFooter, {
      initialState: { health: { status: 'checking' } },
    })

    const dot = wrapper.find('[role="status"]')
    expect(dot.classes()).not.toContain('bg-green-500')
    expect(dot.classes()).not.toContain('bg-red-500')
  })

  it('shows the online status dot and API version when the store reports "online"', async () => {
    const { wrapper } = await renderComponent(AppFooter, {
      initialState: { health: { status: 'online', apiVersion: '3.1.4' } },
    })

    expect(wrapper.find('[role="status"]').classes()).toContain('bg-green-500')
    expect(wrapper.text()).toContain('API v3.1.4')
  })

  it('shows a red status dot when the store reports "offline"', async () => {
    const { wrapper } = await renderComponent(AppFooter, {
      initialState: { health: { status: 'offline' } },
    })

    expect(wrapper.find('[role="status"]').classes()).toContain('bg-red-500')
  })

  it('shows a red status dot when the store reports "error"', async () => {
    const { wrapper } = await renderComponent(AppFooter, {
      initialState: { health: { status: 'error' } },
    })

    expect(wrapper.find('[role="status"]').classes()).toContain('bg-red-500')
  })

  it('formats and shows the API build time when the store has one', async () => {
    const { wrapper } = await renderComponent(AppFooter, {
      initialState: { health: { status: 'online', apiBuildTimestamp: '2026-02-02T00:00:00Z' } },
    })

    expect(wrapper.text()).toMatch(/Build .*2026/)
  })

  it('omits the build-time text when the store has no build timestamp', async () => {
    const { wrapper } = await renderComponent(AppFooter, {
      initialState: { health: { status: 'online', apiBuildTimestamp: null } },
    })

    expect(wrapper.text()).not.toContain('Build ')
  })
})
