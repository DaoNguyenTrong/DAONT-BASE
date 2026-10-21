import { describe, expect, it, vi } from 'vitest'
import { resolveAssetUrl } from '@/lib/asset-url'

vi.mock('@/lib/runtime-config', () => ({
  getRuntimeConfig: () => ({ apiBaseUrl: 'http://api.test:7100/' }),
}))

describe('resolveAssetUrl', () => {
  it('returns undefined when there is no url', () => {
    expect(resolveAssetUrl(null)).toBeUndefined()
    expect(resolveAssetUrl(undefined)).toBeUndefined()
    expect(resolveAssetUrl('')).toBeUndefined()
  })

  it('keeps an absolute url as it is', () => {
    expect(resolveAssetUrl('http://cdn.test/starterkit/public/avatars/a.png')).toBe(
      'http://cdn.test/starterkit/public/avatars/a.png',
    )
  })

  it('roots a relative url at the API origin without doubling the slash', () => {
    expect(resolveAssetUrl('/storage/public/avatars/a.png')).toBe(
      'http://api.test:7100/storage/public/avatars/a.png',
    )
  })
})
