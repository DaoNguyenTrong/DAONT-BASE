import { describe, expect, it } from 'vitest'
import { resolveGuardRedirect } from '@/router'

function route(
  meta: { requiresAuth?: boolean; guestOnly?: boolean },
  extra: { fullPath?: string; query?: Record<string, unknown> } = {},
) {
  return { meta, fullPath: extra.fullPath ?? '/target', query: extra.query ?? {} }
}

const online = { isDown: false }

describe('resolveGuardRedirect', () => {
  it('redirects to login (keeping the destination) when requiresAuth and not authenticated', () => {
    const result = resolveGuardRedirect(
      route({ requiresAuth: true }, { fullPath: '/organizations' }),
      { isAuthenticated: false },
      online,
    )

    expect(result).toEqual({ name: 'login', query: { redirect: '/organizations' } })
  })

  it('redirects to home when guestOnly and already authenticated', () => {
    const result = resolveGuardRedirect(
      route({ guestOnly: true }),
      { isAuthenticated: true },
      online,
    )

    expect(result).toEqual({ name: 'home' })
  })

  it('follows a safe redirect query when guestOnly and already authenticated', () => {
    const result = resolveGuardRedirect(
      route({ guestOnly: true }, { query: { redirect: '/organizations' } }),
      { isAuthenticated: true },
      online,
    )

    expect(result).toBe('/organizations')
  })

  it('ignores an off-origin redirect query and falls back to home', () => {
    const result = resolveGuardRedirect(
      route({ guestOnly: true }, { query: { redirect: '//evil.example/phish' } }),
      { isAuthenticated: true },
      online,
    )

    expect(result).toEqual({ name: 'home' })
  })

  it('allows access to a requiresAuth route when authenticated', () => {
    const result = resolveGuardRedirect(
      route({ requiresAuth: true }),
      { isAuthenticated: true },
      online,
    )

    expect(result).toBeUndefined()
  })

  it('allows access to a guestOnly route when not authenticated', () => {
    const result = resolveGuardRedirect(
      route({ guestOnly: true }),
      { isAuthenticated: false },
      online,
    )

    expect(result).toBeUndefined()
  })

  it('allows access to a route with neither meta flag regardless of auth state', () => {
    expect(resolveGuardRedirect(route({}), { isAuthenticated: false }, online)).toBeUndefined()
    expect(resolveGuardRedirect(route({}), { isAuthenticated: true }, online)).toBeUndefined()
  })

  it('does not redirect to login during an API outage even on a requiresAuth route', () => {
    const result = resolveGuardRedirect(
      route({ requiresAuth: true }),
      { isAuthenticated: false },
      { isDown: true },
    )

    expect(result).toBeUndefined()
  })

  it('does not redirect away from a guestOnly route during an API outage', () => {
    const result = resolveGuardRedirect(
      route({ guestOnly: true }),
      { isAuthenticated: true },
      { isDown: true },
    )

    expect(result).toBeUndefined()
  })
})
