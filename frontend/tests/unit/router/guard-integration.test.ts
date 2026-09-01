import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import router from '@/router'
import { useHealthStore } from '@/stores/health'
import { setupTestPinia } from '../../helpers/pinia'

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
})
