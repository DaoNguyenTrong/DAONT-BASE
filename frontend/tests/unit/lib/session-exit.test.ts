import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { parseQuery } from 'vue-router'

// The module keeps a page-lifetime "already reloading" flag, so each test imports a fresh copy.
async function importFresh() {
  vi.resetModules()
  return import('@/lib/session-exit')
}

describe('session-exit', () => {
  const reload = vi.fn()
  const originalLocation = window.location

  beforeEach(() => {
    reload.mockClear()
    window.history.replaceState(null, '', '/#/organizations')
    Object.defineProperty(window, 'location', {
      configurable: true,
      value: { ...originalLocation, reload },
    })
  })

  afterEach(() => {
    Object.defineProperty(window, 'location', { configurable: true, value: originalLocation })
  })

  it('reloadToLogin rewrites the hash route to /login and reloads the page', async () => {
    const { reloadToLogin } = await importFresh()

    reloadToLogin()

    expect(window.history.state).toBeNull()
    expect(originalLocation.hash).toBe('#/login')
    expect(reload).toHaveBeenCalledTimes(1)
  })

  it('reloadToLogin carries the query (e.g. the redirect target)', async () => {
    const { reloadToLogin } = await importFresh()

    reloadToLogin({ redirect: '/organizations?tab=members&page=2' })

    const [path, search = ''] = originalLocation.hash.slice(1).split(/\?(.*)/s)
    expect(path).toBe('/login')
    // Round-trips through the router's own parser, which is what LoginView reads it with.
    expect(parseQuery(search).redirect).toBe('/organizations?tab=members&page=2')
  })

  it('reloadTo navigates to an arbitrary in-app path', async () => {
    const { reloadTo } = await importFresh()

    reloadTo('/')

    expect(originalLocation.hash).toBe('#/')
    expect(reload).toHaveBeenCalledTimes(1)
  })

  it('lets only the first exit win', async () => {
    const { reloadToLogin, reloadTo } = await importFresh()

    reloadToLogin()
    // e.g. the session-lapse watcher firing right after logout must not redirect elsewhere.
    reloadToLogin({ redirect: '/organizations' })
    reloadTo('/')

    expect(originalLocation.hash).toBe('#/login')
    expect(reload).toHaveBeenCalledTimes(1)
  })
})
