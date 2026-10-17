import { describe, expect, it } from 'vitest'
import { toPagedResult } from '@/lib/paged-result'

describe('toPagedResult', () => {
  it('converts string-widened numeric fields to numbers', () => {
    const result = toPagedResult({
      items: ['a', 'b'],
      totalCount: '42',
      pageNumber: '2',
      pageSize: '20',
      totalPages: '3',
      hasPreviousPage: true,
      hasNextPage: true,
    })

    expect(result).toEqual({
      items: ['a', 'b'],
      totalCount: 42,
      pageNumber: 2,
      pageSize: 20,
      totalPages: 3,
      hasPreviousPage: true,
      hasNextPage: true,
    })
  })

  it('passes numeric fields and the items array through unchanged', () => {
    const items = [{ id: 1 }]

    const result = toPagedResult({
      items,
      totalCount: 1,
      pageNumber: 1,
      pageSize: 10,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false,
    })

    expect(result.items).toBe(items)
    expect(result.totalCount).toBe(1)
    expect(result.totalPages).toBe(1)
  })

  it('defaults the optional computed fields when the payload omits them', () => {
    const result = toPagedResult({ items: [], totalCount: 0, pageNumber: 1, pageSize: 10 })

    expect(result.totalPages).toBe(0)
    expect(result.hasPreviousPage).toBe(false)
    expect(result.hasNextPage).toBe(false)
  })

  it('handles an empty page', () => {
    const result = toPagedResult({ items: [], totalCount: '0', pageNumber: '1', pageSize: '10' })

    expect(result).toEqual({
      items: [],
      totalCount: 0,
      pageNumber: 1,
      pageSize: 10,
      totalPages: 0,
      hasPreviousPage: false,
      hasNextPage: false,
    })
  })
})
