import { describe, expect, it } from 'vitest'
import { GUIDE_HOME, safeGuideTarget, safeRedirectTarget } from '@/lib/safe-redirect'

describe('safeRedirectTarget', () => {
  it('returns app-internal root-relative paths unchanged', () => {
    expect(safeRedirectTarget('/organizations')).toBe('/organizations')
    expect(safeRedirectTarget('/organizations?tab=members#top')).toBe(
      '/organizations?tab=members#top',
    )
  })

  it('rejects non-string and empty values', () => {
    expect(safeRedirectTarget(undefined)).toBeNull()
    expect(safeRedirectTarget(null)).toBeNull()
    expect(safeRedirectTarget(['/a', '/b'])).toBeNull()
    expect(safeRedirectTarget('')).toBeNull()
  })

  it.each([
    ['rejects absolute URLs', ['https://evil.example/phish', 'mailto:someone@example.com']],
    [
      'rejects protocol-relative and backslash-normalised off-origin values',
      ['//evil.example', '/\\evil.example'],
    ],
    ['rejects values that do not start at the root', ['organizations']],
    ['rejects the auth screens themselves', ['/login', '/register?foo=1']],
  ])('%s', (_title, values) => {
    for (const value of values) {
      expect(safeRedirectTarget(value)).toBeNull()
    }
  })
})

describe('safeGuideTarget', () => {
  it('returns guide paths unchanged', () => {
    for (const value of ['/guide', '/guide/', '/guide/organizations/add-member', '/guide/en/faq']) {
      expect(safeGuideTarget(value)).toBe(value)
    }
  })

  it.each([
    ['undefined', undefined],
    ['null', null],
    ['an array', ['/guide/x']],
    ['a number', 42],
    ['an app path', '/organizations'],
    ['a look-alike prefix', '/guidex'],
    ['the root', '/'],
    ['an absolute URL', 'https://evil.example/guide/'],
    ['a protocol-relative URL', '//evil.example'],
    ['a double slash inside the guide', '/guide//evil.example'],
    ['a backslash', '/guide/\\evil'],
    ['a traversal', '/guide/../organizations'],
    ['an encoded traversal', '/guide/%2E%2e/organizations'],
    ['a raw CRLF', '/guide/a\r\nSet-Cookie: x'],
    ['an encoded CRLF', '/guide/%0d%0aSet-Cookie:x'],
    ['an encoded LF in upper case', '/guide/%0A'],
    ['an encoded NUL', '/guide/%00'],
  ])('falls back to the guide home for %s', (_, value) => {
    expect(safeGuideTarget(value)).toBe(GUIDE_HOME)
  })
})
