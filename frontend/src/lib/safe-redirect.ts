/**
 * A `redirect` value pulled from the URL query is attacker-controllable — a
 * crafted `/login?redirect=https://evil.example` link would otherwise bounce the
 * user off-origin right after they authenticate (open redirect).
 *
 * Only follow the value when it is an app-internal, root-relative path. Reject:
 *  - non-strings / empty
 *  - absolute URLs (`https://…`, `mailto:…`)
 *  - protocol-relative (`//host`) and the backslash form browsers normalise to it
 *  - values that don't start at the root (`foo/bar`)
 *  - the auth screens themselves (avoids a pointless post-login hop back to /login)
 */
const AUTH_PATHS = ['/login', '/register']

export function safeRedirectTarget(value: unknown): string | null {
  if (typeof value !== 'string' || value.length === 0) {
    return null
  }

  if (!value.startsWith('/') || value.startsWith('//') || value.startsWith('/\\')) {
    return null
  }

  const path = value.split(/[?#]/)[0] ?? value
  if (AUTH_PATHS.includes(path)) {
    return null
  }

  return value
}
