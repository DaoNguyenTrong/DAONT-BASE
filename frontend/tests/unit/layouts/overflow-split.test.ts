import { describe, expect, it } from 'vitest'
import { splitOverflow } from '@/layouts/overflow-split'

describe('splitOverflow', () => {
  it('keeps every item when they all fit, without reserving room for More', () => {
    expect(splitOverflow([100, 100, 100], 300, 60)).toBe(3)
  })

  it('keeps the leading items that fit next to More', () => {
    expect(splitOverflow([100, 100, 100], 250, 60)).toBe(1)
  })

  it('keeps no item when not even More fits beside the first one', () => {
    expect(splitOverflow([100, 100, 100], 50, 60)).toBe(0)
  })

  it('returns 0 for an empty list', () => {
    expect(splitOverflow([], 250, 60)).toBe(0)
  })
})
