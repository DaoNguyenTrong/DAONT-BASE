import type { PagedResult } from '@/api/types'

export interface UseLazyListOptions<T> {
  pageSize: number
  fetchPage: (pageNumber: number, pageSize: number) => Promise<PagedResult<T>>
}

export function useLazyList<T extends { id: string | number }>(options: UseLazyListOptions<T>) {
  const items = ref<T[]>([]) as Ref<T[]>
  const totalCount = ref(0)
  const pageNumber = ref(0)
  const loading = ref(false)
  const loadingMore = ref(false)

  const hasMore = computed(() => items.value.length < totalCount.value)

  async function loadMore() {
    if (loading.value || loadingMore.value) return
    if (pageNumber.value > 0 && !hasMore.value) return

    const isFirstPage = pageNumber.value === 0
    if (isFirstPage) loading.value = true
    else loadingMore.value = true

    try {
      const result = await options.fetchPage(pageNumber.value + 1, options.pageSize)
      const existingIds = new Set(items.value.map((item) => item.id))
      const newItems = result.items.filter((item) => !existingIds.has(item.id))

      items.value = isFirstPage ? result.items : [...items.value, ...newItems]
      totalCount.value = result.totalCount
      pageNumber.value += 1
    } finally {
      loading.value = false
      loadingMore.value = false
    }
  }

  async function reset() {
    items.value = []
    totalCount.value = 0
    pageNumber.value = 0
    await loadMore()
  }

  return { items, totalCount, loading, loadingMore, hasMore, loadMore, reset }
}
