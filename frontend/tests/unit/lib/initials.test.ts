import { describe, expect, it } from 'vitest'
import { initialsOf } from '@/lib/initials'

describe('initialsOf', () => {
  it.each([
    ['Nguyễn Trọng', 'NT'],
    ['admin', 'A'],
    ['Lê   Văn   An', 'LA'],
    ['  ', '?'],
    [null, '?'],
    [undefined, '?'],
  ])('turns %j into %j', (name, expected) => {
    expect(initialsOf(name)).toBe(expected)
  })
})
