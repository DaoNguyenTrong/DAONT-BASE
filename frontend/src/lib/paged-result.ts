import type { PagedResult } from '@/api/types'

// Generated PagedResultOf*Dto types report int64/int32 fields as `number | string` (the .NET
// OpenAPI generator widens them for JS-safe-integer interop) and mark totalPages/hasPreviousPage/
// hasNextPage optional (computed properties don't get inferred as required). Normalize back to
// the stable, number-only PagedResult<T> shape every list consumer expects.
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
