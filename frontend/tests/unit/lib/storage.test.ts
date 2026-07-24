import { describe, expect, it } from 'vitest'
import { getItem, removeItem, setItem } from '@/lib/storage'

describe('storage', () => {
  it('returns null for a missing key', () => {
    expect(getItem('missing-key')).toBeNull()
  })

  it('roundtrips a value via setItem/getItem', () => {
    setItem('greeting', 'hello')
    expect(getItem('greeting')).toBe('hello')
  })

  it('deletes a previously-set key via removeItem', () => {
    setItem('greeting', 'hello')
    removeItem('greeting')
    expect(getItem('greeting')).toBeNull()
  })
})
