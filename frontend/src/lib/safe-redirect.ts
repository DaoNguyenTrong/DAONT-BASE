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

/**
 * nginx sends a reader without a session to `/#/guide-auth?next=<path>`, and that view
 * sends them back with a full page load. `next` is attacker-controllable, so only a path
 * inside the user guide is followed; anything else opens the guide's home page. Rejected:
 *  - paths outside `/guide/` (an app route, `/guidex`, absolute and protocol-relative URLs)
 *  - `//` and `\` anywhere (browsers resolve them to another host) and `..`, raw or as `%2e`
 *    (climbs out of the guide)
 *  - control characters, raw or percent-encoded (CR/LF would split the redirect's headers)
 */
export const GUIDE_HOME = '/guide/'

export function safeGuideTarget(value: unknown): string {
  if (typeof value !== 'string') return GUIDE_HOME
  if (value !== '/guide' && !value.startsWith(GUIDE_HOME)) return GUIDE_HOME
  if (value.includes('//') || value.includes('\\') || value.includes('..')) return GUIDE_HOME
  if (/%2e/i.test(value)) return GUIDE_HOME
  if (/[\u0000-\u001f\u007f]|%[01][0-9a-f]|%7f/i.test(value)) return GUIDE_HOME
  return value
}
