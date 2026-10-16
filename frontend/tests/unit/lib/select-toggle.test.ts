import { describe, expect, it } from 'vitest'
import { resolveSelectUpdate } from '@/lib/select-toggle'

describe('resolveSelectUpdate', () => {
  it('clears a single select when the selected option is picked again', () => {
    expect(resolveSelectUpdate('north', 'north')).toBeNull()
  })

  it.each([
    ['a different option', 'north', 'south', 'south'],
    ['a first pick', null, 'north', 'north'],
    ['a first pick over undefined', undefined, 'north', 'north'],
    ['the clear button', 'north', null, null],
  ])('passes %s through', (_name, current, next, expected) => {
    expect(resolveSelectUpdate(current, next)).toBe(expected)
  })

  // Naive UI already toggles inside a multiple select; the array it emits is final.
  it('leaves a multiple select untouched', () => {
    expect(resolveSelectUpdate(['a'], ['a', 'b'], true)).toEqual(['a', 'b'])
  })
})
