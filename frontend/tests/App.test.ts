import { http, HttpResponse } from 'msw'
import { flushPromises } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import App from '@/App.vue'
import { useAuthStore } from '@/stores/auth'
import { markSessionHint } from '@/lib/auth-session'
import { server } from './helpers/msw/server'
import { makeAccount, makeAuthResponse } from './helpers/msw/fixtures'
import { renderComponent } from './helpers/render'

// No fake timers here — let real macrotasks drain so MSW's fetch responses and
// the chained recovery watcher (restoreSession → router.replace) settle.
async function settle() {
  for (let i = 0; i < 5; i++) {
    await new Promise((resolve) => setTimeout(resolve, 0))
    await flushPromises()
  }
}

const stubs = {
  RouterView: { template: '<div class="router-view-stub" />' },
  ServerErrorScreen: { template: '<div class="server-error-stub" />' },
}

describe('App', () => {
  it('renders the routed view while the API is reachable', async () => {
    const { wrapper } = await renderComponent(App, { global: { stubs } })

    expect(wrapper.find('.router-view-stub').exists()).toBe(true)
    expect(wrapper.find('.server-error-stub').exists()).toBe(false)

    wrapper.unmount()
  })

  it('replaces the routed view with the server-error screen once health is "error"', async () => {
    // Keep the poll pending so it can't flip the seeded state back to online.
    server.use(http.get('*/api/health', () => new Promise<Response>(() => {})))

    const { wrapper } = await renderComponent(App, {
      initialState: { health: { status: 'error' } },
      global: { stubs },
    })

    expect(wrapper.find('.server-error-stub').exists()).toBe(true)
    expect(wrapper.find('.router-view-stub').exists()).toBe(false)

    wrapper.unmount()
  })

  it('restores the session and routes home once the API recovers', async () => {
    // This browser authenticated before the outage, so the recovery watcher's
    // restoreSession() is allowed to attempt a silent refresh.
    markSessionHint()
    server.use(http.post('*/api/auth/refresh', () => HttpResponse.json(makeAuthResponse())))

    // Booted on /login while "down"; the default health handler is healthy, so
    // the first poll flips isDown false and the recovery watcher takes over.
    const { wrapper, router } = await renderComponent(App, {
      initialRoute: '/login',
      initialState: { health: { status: 'error' } },
      global: { stubs },
    })

    await settle()

    expect(router.currentRoute.value.name).toBe('home')
    expect(wrapper.find('.router-view-stub').exists()).toBe(true)

    wrapper.unmount()
  })

  it('bounces to login (keeping the destination) when the session lapses on a protected route', async () => {
    server.use(http.get('*/api/health', () => new Promise<Response>(() => {})))

    const { wrapper, router } = await renderComponent(App, {
      initialRoute: '/organizations',
      initialState: { auth: { account: makeAccount() } },
      global: { stubs },
    })

    expect(router.currentRoute.value.name).toBe('organizations')

    // Mirror the client interceptor clearing auth after a failed silent refresh.
    useAuthStore().clearAuth()
    await settle()

    expect(router.currentRoute.value.name).toBe('login')
    expect(router.currentRoute.value.query.redirect).toBe('/organizations')

    wrapper.unmount()
  })
})
