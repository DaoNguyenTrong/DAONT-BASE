/**
 * `redirect` is attacker-controllable (open redirect): only a root-relative app path is followed.
 * Rejects non-strings/empty, absolute (`https:`, `mailto:`) and non-root (`foo/bar`) values,
 * `//host` and the `/\` form browsers normalise to it, and the auth screens (no hop to /login).
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
 * `next` from nginx's `/#/guide-auth` is attacker-controllable: a path outside `/guide/`, any `//`
 * or `\` (another host), `..` raw or as `%2e` (escapes the guide), or a control character raw or
 * percent-encoded (CR/LF splits the redirect's headers) falls back to the guide's home page.
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
