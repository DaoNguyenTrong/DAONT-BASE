import type { PagedResult } from '@/api/types'

// The .NET OpenAPI generator widens integers to `number | string` and marks computed properties
// (totalPages, hasPreviousPage, hasNextPage) optional; normalize to the number-only PagedResult<T>.
interface RawPagedResult<T> {
  items: T[]
  totalCount: number | string
  pageNumber: number | string
  pageSize: number | string
  totalPages?: number | string
  hasPreviousPage?: boolean
  hasNextPage?: boolean
}

export function toPagedResult<T>(raw: RawPagedResult<T>): PagedResult<T> {
  return {
    items: raw.items,
    totalCount: Number(raw.totalCount),
    pageNumber: Number(raw.pageNumber),
    pageSize: Number(raw.pageSize),
    totalPages: Number(raw.totalPages ?? 0),
    hasPreviousPage: raw.hasPreviousPage ?? false,
    hasNextPage: raw.hasNextPage ?? false,
  }
}
