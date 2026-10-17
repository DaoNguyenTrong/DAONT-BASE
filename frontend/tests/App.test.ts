import { http, HttpResponse } from 'msw'
import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import { reloadToLogin } from '@/lib/session-exit'
import App from '@/App.vue'
import { useAuthStore } from '@/stores/auth'
import { markSessionHint } from '@/lib/auth-session'
import { server } from './helpers/msw/server'
import { makeAccount, makeAuthResponse } from './helpers/msw/fixtures'
import { renderComponent } from './helpers/render'
import { storeIn } from './helpers/pinia'

vi.mock('@/lib/session-exit', () => ({
  reloadTo: vi.fn(),
  reloadToLogin: vi.fn(),
}))

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

  it('on an explicit logout, reloads to a plain login — never with a redirect into the old page', async () => {
    server.use(
      http.get('*/api/health', () => new Promise<Response>(() => {})),
      http.post('*/api/auth/logout', () => new HttpResponse(null, { status: 204 })),
    )
    vi.mocked(reloadToLogin).mockClear()

    const { wrapper, pinia } = await renderComponent(App, {
      initialRoute: '/organizations',
      initialState: { auth: { account: makeAccount() } },
      global: { stubs },
    })

    await storeIn(pinia, useAuthStore).logout()
    await settle()

    // The lapse watcher also fires (isAuthenticated true → false); the logout's exit must come
    // first so the next account to sign in isn't sent to the previous account's page.
    expect(vi.mocked(reloadToLogin).mock.calls[0]).toEqual([])

    wrapper.unmount()
  })

  it('reloads to login (keeping the destination) when the session lapses on a protected route', async () => {
    server.use(http.get('*/api/health', () => new Promise<Response>(() => {})))

    const { wrapper, router, pinia } = await renderComponent(App, {
      initialRoute: '/organizations',
      initialState: { auth: { account: makeAccount() } },
      global: { stubs },
    })

    expect(router.currentRoute.value.name).toBe('organizations')

    // Mirror the client interceptor clearing auth after a failed silent refresh.
    storeIn(pinia, useAuthStore).clearAuth()
    await settle()

    // A full reload, not router.replace: no store may outlive the account that just lapsed.
    expect(reloadToLogin).toHaveBeenCalledWith({ redirect: '/organizations' })

    wrapper.unmount()
  })
})
