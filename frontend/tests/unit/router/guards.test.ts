import { describe, expect, it } from 'vitest'
import { resolveGuardRedirect } from '@/router'

function route(meta: { requiresAuth?: boolean; guestOnly?: boolean }) {
  return { meta }
}

describe('resolveGuardRedirect', () => {
  it('redirects to login when requiresAuth and not authenticated', () => {
    const result = resolveGuardRedirect(route({ requiresAuth: true }), { isAuthenticated: false })

    expect(result).toEqual({ name: 'login' })
  })

  it('redirects to home when guestOnly and already authenticated', () => {
    const result = resolveGuardRedirect(route({ guestOnly: true }), { isAuthenticated: true })

    expect(result).toEqual({ name: 'home' })
  })

  it('allows access to a requiresAuth route when authenticated', () => {
    const result = resolveGuardRedirect(route({ requiresAuth: true }), { isAuthenticated: true })

    expect(result).toBeUndefined()
  })

  it('allows access to a guestOnly route when not authenticated', () => {
    const result = resolveGuardRedirect(route({ guestOnly: true }), { isAuthenticated: false })

    expect(result).toBeUndefined()
  })

  it('allows access to a route with neither meta flag regardless of auth state', () => {
    expect(resolveGuardRedirect(route({}), { isAuthenticated: false })).toBeUndefined()
    expect(resolveGuardRedirect(route({}), { isAuthenticated: true })).toBeUndefined()
  })
})
