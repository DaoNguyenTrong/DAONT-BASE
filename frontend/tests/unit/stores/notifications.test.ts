import { http, HttpResponse } from 'msw'
import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest'
import { useNotificationsStore } from '@/stores/notifications'
import type { NotificationDto } from '@/api/generated/model/notificationDto'
import { setupTestPinia } from '../../helpers/pinia'
import { server } from '../../helpers/msw/server'

function flushHttp() {
  return new Promise((resolve) => setTimeout(resolve, 0))
}

function makeNotification(overrides: Partial<NotificationDto> = {}): NotificationDto {
  return {
    id: 'notif-1',
    type: 'OrganizationMemberAdded',
    data: null,
    isRead: false,
    createdAt: '2026-08-04T00:00:00Z',
    ...overrides,
  }
}

describe('useNotificationsStore', () => {
  beforeEach(() => {
    setupTestPinia()
  })

  it('fetchUnreadCount stores the normalized count', async () => {
    server.use(http.get('*/api/notifications/unread-count', () => HttpResponse.json({ count: 3 })))
    const store = useNotificationsStore()

    await store.fetchUnreadCount()

    expect(store.unreadCount).toBe(3)
  })

  it('fetchList returns the normalized paged result', async () => {
    server.use(
      http.get('*/api/notifications', () =>
        HttpResponse.json({
          items: [makeNotification()],
          totalCount: 1,
          pageNumber: 1,
          pageSize: 10,
          totalPages: 1,
          hasPreviousPage: false,
          hasNextPage: false,
        }),
      ),
    )
    const store = useNotificationsStore()

    const result = await store.fetchList(1, 10)

    expect(result.items).toHaveLength(1)
    expect(result.items[0].id).toBe('notif-1')
    expect(result.totalCount).toBe(1)
  })

  it('markAsRead calls the endpoint then refreshes the unread count', async () => {
    let markedId: string | null = null
    server.use(
      http.patch('*/api/notifications/:id/read', ({ params }) => {
        markedId = params.id as string
        return new HttpResponse(null, { status: 204 })
      }),
      http.get('*/api/notifications/unread-count', () => HttpResponse.json({ count: 0 })),
    )
    const store = useNotificationsStore()

    await store.markAsRead('notif-1')

    expect(markedId).toBe('notif-1')
    expect(store.unreadCount).toBe(0)
  })

  it('markAllAsRead calls the endpoint then refreshes the unread count', async () => {
    const hits = vi.fn()
    server.use(
      http.patch('*/api/notifications/read-all', () => {
        hits()
        return new HttpResponse(null, { status: 204 })
      }),
      http.get('*/api/notifications/unread-count', () => HttpResponse.json({ count: 0 })),
    )
    const store = useNotificationsStore()

    await store.markAllAsRead()

    expect(hits).toHaveBeenCalledTimes(1)
    expect(store.unreadCount).toBe(0)
  })

  describe('polling', () => {
    beforeEach(() => {
      vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] })
    })

    afterEach(() => {
      vi.useRealTimers()
      Object.defineProperty(document, 'visibilityState', { value: 'visible', configurable: true })
    })

    it('fetches immediately and re-polls every 30s while visible', async () => {
      const hits = vi.fn()
      server.use(
        http.get('*/api/notifications/unread-count', () => {
          hits()
          return HttpResponse.json({ count: hits.mock.calls.length })
        }),
      )
      const store = useNotificationsStore()

      store.startPolling()
      await flushHttp()
      expect(hits).toHaveBeenCalledTimes(1)
      expect(store.unreadCount).toBe(1)

      await vi.advanceTimersByTimeAsync(30_000)
      await flushHttp()
      expect(hits).toHaveBeenCalledTimes(2)

      store.stopPolling()
    })

    it('stops polling on stopPolling', async () => {
      const hits = vi.fn()
      server.use(
        http.get('*/api/notifications/unread-count', () => {
          hits()
          return HttpResponse.json({ count: 0 })
        }),
      )
      const store = useNotificationsStore()

      store.startPolling()
      await flushHttp()
      expect(hits).toHaveBeenCalledTimes(1)

      store.stopPolling()
      await vi.advanceTimersByTimeAsync(60_000)
      await flushHttp()
      expect(hits).toHaveBeenCalledTimes(1)
    })

    it('pauses when the tab becomes hidden and resumes with an immediate fetch when visible again', async () => {
      const hits = vi.fn()
      server.use(
        http.get('*/api/notifications/unread-count', () => {
          hits()
          return HttpResponse.json({ count: 0 })
        }),
      )
      const store = useNotificationsStore()

      store.startPolling()
      await flushHttp()
      expect(hits).toHaveBeenCalledTimes(1)

      Object.defineProperty(document, 'visibilityState', { value: 'hidden', configurable: true })
      document.dispatchEvent(new Event('visibilitychange'))
      await vi.advanceTimersByTimeAsync(60_000)
      await flushHttp()
      expect(hits).toHaveBeenCalledTimes(1)

      Object.defineProperty(document, 'visibilityState', { value: 'visible', configurable: true })
      document.dispatchEvent(new Event('visibilitychange'))
      await flushHttp()
      expect(hits).toHaveBeenCalledTimes(2)

      store.stopPolling()
    })
  })
})
