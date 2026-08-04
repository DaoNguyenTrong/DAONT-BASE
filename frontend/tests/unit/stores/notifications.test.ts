import { http, HttpResponse } from 'msw'
import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest'
import { useNotificationsStore } from '@/stores/notifications'
import type { NotificationDto } from '@/api/generated/model/notificationDto'
import { setupTestPinia } from '../../helpers/pinia'
import { server } from '../../helpers/msw/server'

// vi.hoisted: vi.mock's factory below is hoisted above these imports/consts by vitest, so the
// fake connection must be defined via vi.hoisted to be visible inside the factory.
const { fakeConnection, listeners, startMock, stopMock } = vi.hoisted(() => {
  const listeners: Record<string, (...args: unknown[]) => void> = {}
  const startMock = vi.fn(() => Promise.resolve())
  const stopMock = vi.fn(() => Promise.resolve())
  const fakeConnection = {
    on: (event: string, handler: (...args: unknown[]) => void) => {
      listeners[event] = handler
    },
    onreconnecting: (handler: () => void) => {
      listeners.reconnecting = handler
    },
    onreconnected: (handler: () => void) => {
      listeners.reconnected = handler
    },
    onclose: (handler: () => void) => {
      listeners.close = handler
    },
    start: startMock,
    stop: stopMock,
  }
  return { fakeConnection, listeners, startMock, stopMock }
})

vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: class {
    withUrl() {
      return this
    }
    withAutomaticReconnect() {
      return this
    }
    build() {
      return fakeConnection
    }
  },
}))

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

  describe('realtime', () => {
    beforeEach(() => {
      vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] })
      startMock.mockClear()
      stopMock.mockClear()
    })

    afterEach(() => {
      vi.useRealTimers()
    })

    it('connectRealtime starts the hub connection and pauses the poll fallback once connected', async () => {
      server.use(
        http.get('*/api/notifications/unread-count', () => HttpResponse.json({ count: 0 })),
      )
      const store = useNotificationsStore()
      store.startPolling()
      await flushHttp()

      await store.connectRealtime()

      expect(startMock).toHaveBeenCalledTimes(1)

      const hits = vi.fn()
      server.use(
        http.get('*/api/notifications/unread-count', () => {
          hits()
          return HttpResponse.json({ count: 0 })
        }),
      )
      await vi.advanceTimersByTimeAsync(30_000)
      await flushHttp()
      expect(hits).not.toHaveBeenCalled()

      store.stopPolling()
      await store.disconnectRealtime()
    })

    it('increments unreadCount when ReceiveNotification fires', async () => {
      server.use(
        http.get('*/api/notifications/unread-count', () => HttpResponse.json({ count: 0 })),
      )
      const store = useNotificationsStore()
      await store.connectRealtime()

      listeners.ReceiveNotification(makeNotification())

      expect(store.unreadCount).toBe(1)

      await store.disconnectRealtime()
    })

    it('resumes the poll fallback immediately on onreconnecting, before onclose fires', async () => {
      server.use(
        http.get('*/api/notifications/unread-count', () => HttpResponse.json({ count: 0 })),
      )
      const store = useNotificationsStore()
      await store.connectRealtime()

      const hits = vi.fn()
      server.use(
        http.get('*/api/notifications/unread-count', () => {
          hits()
          return HttpResponse.json({ count: 0 })
        }),
      )

      listeners.reconnecting()
      await flushHttp()
      expect(hits).toHaveBeenCalledTimes(1) // resume() fetches immediately

      await vi.advanceTimersByTimeAsync(30_000)
      await flushHttp()
      expect(hits).toHaveBeenCalledTimes(2) // poll interval resumed while reconnecting

      await store.disconnectRealtime()
    })

    it('pauses the poll fallback again and catches up via REST on onreconnected', async () => {
      server.use(
        http.get('*/api/notifications/unread-count', () => HttpResponse.json({ count: 5 })),
      )
      const store = useNotificationsStore()
      await store.connectRealtime()
      listeners.reconnecting()
      await flushHttp()

      const hits = vi.fn()
      server.use(
        http.get('*/api/notifications/unread-count', () => {
          hits()
          return HttpResponse.json({ count: 7 })
        }),
      )
      listeners.reconnected()
      await flushHttp()

      expect(store.unreadCount).toBe(7) // catch-up fetch reconciles what may have been missed
      expect(hits).toHaveBeenCalledTimes(1)

      hits.mockClear()
      await vi.advanceTimersByTimeAsync(30_000)
      await flushHttp()
      expect(hits).not.toHaveBeenCalled() // poll paused again now that realtime is back

      await store.disconnectRealtime()
    })

    it('resumes the poll fallback on onclose', async () => {
      server.use(
        http.get('*/api/notifications/unread-count', () => HttpResponse.json({ count: 0 })),
      )
      const store = useNotificationsStore()
      await store.connectRealtime()

      const hits = vi.fn()
      server.use(
        http.get('*/api/notifications/unread-count', () => {
          hits()
          return HttpResponse.json({ count: 0 })
        }),
      )

      listeners.close()
      await flushHttp()
      expect(hits).toHaveBeenCalledTimes(1)

      await vi.advanceTimersByTimeAsync(30_000)
      await flushHttp()
      expect(hits).toHaveBeenCalledTimes(2)
    })

    it('disconnectRealtime stops the hub connection', async () => {
      const store = useNotificationsStore()
      await store.connectRealtime()

      await store.disconnectRealtime()

      expect(stopMock).toHaveBeenCalledTimes(1)
    })
  })
})
