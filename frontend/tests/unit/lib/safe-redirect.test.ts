import { describe, expect, it } from 'vitest'
import { safeRedirectTarget } from '@/lib/safe-redirect'

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
