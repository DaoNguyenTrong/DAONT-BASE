import { describe, expect, it } from 'vitest'
import { isServerOutage } from '@/lib/api-outage'
import { ApiError } from '@/api/types'

function apiError(status: number): ApiError {
  return new ApiError({ status, title: 'Error' })
}

function axiosShaped(status: unknown): { response: { status: unknown } } {
  return { response: { status } }
}

describe('isServerOutage', () => {
  it.each([500, 502, 503, 504])('treats an ApiError with status %i as an outage', (status) => {
    expect(isServerOutage(apiError(status))).toBe(true)
  })

  it.each([400, 401, 403, 404, 422, 429, 499])(
    'does not treat an ApiError with status %i as an outage',
    (status) => {
      expect(isServerOutage(apiError(status))).toBe(false)
    },
  )

  it('reads the status of an axios-shaped error with a response', () => {
    expect(isServerOutage(axiosShaped(503))).toBe(true)
    expect(isServerOutage(axiosShaped(500))).toBe(true)
    expect(isServerOutage(axiosShaped(499))).toBe(false)
    expect(isServerOutage(axiosShaped(401))).toBe(false)
  })

  it('treats an error with no response as an outage (network, DNS, connection refused)', () => {
    expect(isServerOutage(new Error('Network Error'))).toBe(true)
    expect(isServerOutage({ response: undefined })).toBe(true)
    expect(isServerOutage({ response: {} })).toBe(true)
  })

  it('treats a non-numeric status as an outage', () => {
    expect(isServerOutage(axiosShaped('500'))).toBe(true)
    expect(isServerOutage(axiosShaped(null))).toBe(true)
  })

  it('treats null, undefined and non-object values as an outage', () => {
    expect(isServerOutage(null)).toBe(true)
    expect(isServerOutage(undefined)).toBe(true)
    expect(isServerOutage('boom')).toBe(true)
    expect(isServerOutage(42)).toBe(true)
  })
})
