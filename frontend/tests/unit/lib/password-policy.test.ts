import { describe, expect, it } from 'vitest'
import { PASSWORD_MIN_LENGTH } from '@/lib/password-policy'

describe('PASSWORD_MIN_LENGTH', () => {
  it('matches the backend password policy minimum of 8 characters', () => {
    expect(PASSWORD_MIN_LENGTH).toBe(8)
  })
})
