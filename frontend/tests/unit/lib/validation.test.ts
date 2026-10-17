import { describe, expect, it } from 'vitest'
import { mapValidationErrors } from '@/lib/validation'

describe('mapValidationErrors', () => {
  it('returns an empty object when there are no errors', () => {
    expect(mapValidationErrors(undefined)).toEqual({})
    expect(mapValidationErrors({})).toEqual({})
  })

  it('lowercases the whole field name rather than camel-casing it', () => {
    expect(mapValidationErrors({ Email: ['Invalid'], NewPassword: ['Too short'] })).toEqual({
      email: 'Invalid',
      newpassword: 'Too short',
    })
  })

  it('keeps only the first message of each field', () => {
    expect(mapValidationErrors({ Name: ['First', 'Second'] })).toEqual({ name: 'First' })
  })

  it('maps a field with no messages to an empty string', () => {
    expect(mapValidationErrors({ Name: [] })).toEqual({ name: '' })
  })

  it('lets a later field win when two names differ only by case', () => {
    expect(mapValidationErrors({ Email: ['Upper'], email: ['Lower'] })).toEqual({ email: 'Lower' })
  })
})
