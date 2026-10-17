import type { RouteLocationNormalized } from 'vue-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import router from '@/router'
import { useHealthStore } from '@/stores/health'
import { setupTestPinia } from '../../helpers/pinia'
import { stubView } from '../../helpers/router-view-stubs'

// This file checks the guards, not the views: stub every lazy view it navigates to.
vi.mock('@/layouts/AppLayout.vue', () => stubView())
vi.mock('@/views/HomeView.vue', () => stubView())
vi.mock('@/views/OrganizationsView.vue', () => stubView())
vi.mock('@/views/LoginView.vue', () => stubView())

// Exercises the real router's `beforeEach` wiring (not just the pure
// `resolveGuardRedirect`) — this is the seam that redirected an F5-with-BE-down
// visitor to /login.
describe('router guard (wired, real router)', () => {
  beforeEach(() => {
    setupTestPinia()
  })

  afterEach(async () => {
    await router.replace('/login')
  })

  it('redirects an unauthenticated visitor on a requiresAuth route to /login, keeping the destination', async () => {
    await router.push('/organizations')

    expect(router.currentRoute.value.name).toBe('login')
    expect(router.currentRoute.value.query.redirect).toBe('/organizations')
  })

  it('keeps an unauthenticated visitor on the intended route during an API outage', async () => {
    useHealthStore().reportOutage()

    await router.push('/')

    expect(router.currentRoute.value.name).toBe('home')
  })

  it('scrolls the window to the top on navigation, or back to the saved position on back/forward', async () => {
    // `resolve()` lacks only `redirectedFrom`, which scrollBehavior doesn't read.
    const to = router.resolve('/register') as RouteLocationNormalized
    const from = router.resolve('/login') as RouteLocationNormalized

    expect(await router.options.scrollBehavior?.(to, from, null)).toEqual({ top: 0 })
    expect(await router.options.scrollBehavior?.(to, from, { left: 0, top: 320 })).toEqual({
      left: 0,
      top: 320,
    })
  })
})
