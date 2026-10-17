import { describe, expect, it } from 'vitest'
import router, { resolveGuardRedirect } from '@/router'

function route(
  meta: { requiresAuth?: boolean; guestOnly?: boolean; permission?: string },
  extra: { fullPath?: string; query?: Record<string, unknown> } = {},
) {
  return { meta, fullPath: extra.fullPath ?? '/target', query: extra.query ?? {} }
}

const online = { isDown: false }

function signedIn(permissions: string[] = []) {
  return { isAuthenticated: true, hasPermission: (code: string) => permissions.includes(code) }
}

const signedOut = { isAuthenticated: false, hasPermission: () => false }

describe('resolveGuardRedirect', () => {
  // Hiding the menu item is a convenience; a typed URL or an old bookmark still has to be stopped.
  it('sends a signed-in user without the route permission to home', () => {
    const result = resolveGuardRedirect(
      route({ requiresAuth: true, permission: 'billing.manage' }),
      signedIn(['reports.view']),
      online,
    )

    expect(result).toEqual({ name: 'home' })
  })

  it('lets a signed-in user with the route permission through', () => {
    const result = resolveGuardRedirect(
      route({ requiresAuth: true, permission: 'billing.manage' }),
      signedIn(['billing.manage']),
      online,
    )

    expect(result).toBeUndefined()
  })

  // Login comes first: the permission is only knowable once there is a session.
  it('sends a signed-out user on a gated route to login, keeping the destination', () => {
    const result = resolveGuardRedirect(
      route({ requiresAuth: true, permission: 'billing.manage' }, { fullPath: '/billing' }),
      signedOut,
      online,
    )

    expect(result).toEqual({ name: 'login', query: { redirect: '/billing' } })
  })

  it('redirects to login (keeping the destination) when requiresAuth and not authenticated', () => {
    const result = resolveGuardRedirect(
      route({ requiresAuth: true }, { fullPath: '/organizations' }),
      signedOut,
      online,
    )

    expect(result).toEqual({ name: 'login', query: { redirect: '/organizations' } })
  })

  it('redirects to home when guestOnly and already authenticated', () => {
    const result = resolveGuardRedirect(route({ guestOnly: true }), signedIn(), online)

    expect(result).toEqual({ name: 'home' })
  })

  it('follows a safe redirect query when guestOnly and already authenticated', () => {
    const result = resolveGuardRedirect(
      route({ guestOnly: true }, { query: { redirect: '/organizations' } }),
      signedIn(),
      online,
    )

    expect(result).toBe('/organizations')
  })

  it('ignores an off-origin redirect query and falls back to home', () => {
    const result = resolveGuardRedirect(
      route({ guestOnly: true }, { query: { redirect: '//evil.example/phish' } }),
      signedIn(),
      online,
    )

    expect(result).toEqual({ name: 'home' })
  })

  it('allows access to a requiresAuth route when authenticated', () => {
    const result = resolveGuardRedirect(route({ requiresAuth: true }), signedIn(), online)

    expect(result).toBeUndefined()
  })

  it('allows access to a guestOnly route when not authenticated', () => {
    const result = resolveGuardRedirect(route({ guestOnly: true }), signedOut, online)

    expect(result).toBeUndefined()
  })

  it('allows access to a route with neither meta flag regardless of auth state', () => {
    expect(resolveGuardRedirect(route({}), signedOut, online)).toBeUndefined()
    expect(resolveGuardRedirect(route({}), signedIn(), online)).toBeUndefined()
  })

  it('does not redirect to login during an API outage even on a requiresAuth route', () => {
    const result = resolveGuardRedirect(route({ requiresAuth: true }), signedOut, { isDown: true })

    expect(result).toBeUndefined()
  })

  it('does not redirect away from a guestOnly route during an API outage', () => {
    const result = resolveGuardRedirect(route({ guestOnly: true }), signedIn(), { isDown: true })

    expect(result).toBeUndefined()
  })
})

describe('routes', () => {
  // nginx sends a reader without a session here; the guard must route them through login first.
  it('requires a session for the guide hand-off route', () => {
    expect(router.resolve('/guide-auth').meta.requiresAuth).toBe(true)
  })
})
