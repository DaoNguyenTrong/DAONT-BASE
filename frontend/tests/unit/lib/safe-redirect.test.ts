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

  it('rejects absolute URLs', () => {
    expect(safeRedirectTarget('https://evil.example/phish')).toBeNull()
    expect(safeRedirectTarget('mailto:someone@example.com')).toBeNull()
  })

  it('rejects protocol-relative and backslash-normalised off-origin values', () => {
    expect(safeRedirectTarget('//evil.example')).toBeNull()
    expect(safeRedirectTarget('/\\evil.example')).toBeNull()
  })

  it('rejects values that do not start at the root', () => {
    expect(safeRedirectTarget('organizations')).toBeNull()
  })

  it('rejects the auth screens themselves', () => {
    expect(safeRedirectTarget('/login')).toBeNull()
    expect(safeRedirectTarget('/register?foo=1')).toBeNull()
  })
})
