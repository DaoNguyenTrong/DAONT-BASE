import { describe, expect, it, vi } from 'vitest'
import { useLazyList } from '@/composables/use-lazy-list'
import type { PagedResult } from '@/api/types'

type Item = { id: string }

function makePage(items: Item[], totalCount: number): PagedResult<Item> {
  return {
    items,
    totalCount,
    pageNumber: 0,
    pageSize: 10,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false,
  }
}

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason?: unknown) => void
  const promise = new Promise<T>((res, rej) => {
    resolve = res
    reject = rej
  })
  return { promise, resolve, reject }
}

describe('useLazyList', () => {
  it('sets loading (not loadingMore) while fetching the first page', async () => {
    const d = deferred<PagedResult<Item>>()
    const fetchPage = vi.fn(() => d.promise)
    const list = useLazyList({ pageSize: 10, fetchPage })

    const pending = list.loadMore()
    expect(list.loading.value).toBe(true)
    expect(list.loadingMore.value).toBe(false)

    d.resolve(makePage([{ id: '1' }], 1))
    await pending

    expect(list.loading.value).toBe(false)
    expect(list.items.value).toEqual([{ id: '1' }])
    expect(list.totalCount.value).toBe(1)
    expect(fetchPage).toHaveBeenCalledWith(1, 10)
  })

  it('sets loadingMore (not loading) while fetching a subsequent page', async () => {
    const fetchPage = vi
      .fn<UseLazyListFetch>()
      .mockResolvedValueOnce(makePage([{ id: '1' }], 2))
    const list = useLazyList({ pageSize: 10, fetchPage })

    await list.loadMore()

    const d = deferred<PagedResult<Item>>()
    fetchPage.mockReturnValueOnce(d.promise)
    const pending = list.loadMore()

    expect(list.loadingMore.value).toBe(true)
    expect(list.loading.value).toBe(false)

    d.resolve(makePage([{ id: '2' }], 2))
    await pending

    expect(list.loadingMore.value).toBe(false)
    expect(fetchPage).toHaveBeenLastCalledWith(2, 10)
  })

  it('no-ops when a load is already in flight (re-entrancy guard)', async () => {
    const d = deferred<PagedResult<Item>>()
    const fetchPage = vi.fn(() => d.promise)
    const list = useLazyList({ pageSize: 10, fetchPage })

    const first = list.loadMore()
    const second = list.loadMore()

    expect(fetchPage).toHaveBeenCalledTimes(1)

    d.resolve(makePage([{ id: '1' }], 1))
    await Promise.all([first, second])
  })

  it('stops loading further pages once hasMore is false', async () => {
    const fetchPage = vi
      .fn<UseLazyListFetch>()
      .mockResolvedValue(makePage([{ id: '1' }], 1))
    const list = useLazyList({ pageSize: 10, fetchPage })

    await list.loadMore()
    expect(list.hasMore.value).toBe(false)

    await list.loadMore()
    expect(fetchPage).toHaveBeenCalledTimes(1)
  })

  it('dedupes appended items by id', async () => {
    const fetchPage = vi
      .fn<UseLazyListFetch>()
      .mockResolvedValueOnce(makePage([{ id: '1' }, { id: '2' }], 4))
      .mockResolvedValueOnce(makePage([{ id: '2' }, { id: '3' }], 4))
    const list = useLazyList({ pageSize: 10, fetchPage })

    await list.loadMore()
    await list.loadMore()

    expect(list.items.value).toEqual([{ id: '1' }, { id: '2' }, { id: '3' }])
  })

  it('clears state and reloads page one on reset', async () => {
    const fetchPage = vi
      .fn<UseLazyListFetch>()
      .mockResolvedValueOnce(makePage([{ id: '1' }, { id: '2' }], 5))
      .mockResolvedValueOnce(makePage([{ id: '9' }], 1))
    const list = useLazyList({ pageSize: 10, fetchPage })

    await list.loadMore()
    await list.reset()

    expect(list.items.value).toEqual([{ id: '9' }])
    expect(list.totalCount.value).toBe(1)
    expect(fetchPage).toHaveBeenLastCalledWith(1, 10)
  })

  it('resets loading flags in finally even when fetchPage rejects', async () => {
    const fetchPage = vi.fn<UseLazyListFetch>().mockRejectedValue(new Error('boom'))
    const list = useLazyList({ pageSize: 10, fetchPage })

    await expect(list.loadMore()).rejects.toThrow('boom')

    expect(list.loading.value).toBe(false)
    expect(list.loadingMore.value).toBe(false)
  })
})

type UseLazyListFetch = (pageNumber: number, pageSize: number) => Promise<PagedResult<Item>>
