import { http, HttpResponse } from 'msw'
import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest'
import { useNotificationsStore } from '@/stores/notifications'
import type { NotificationDto } from '@/api/generated/model/notificationDto'
import { setupTestPinia } from '../../helpers/pinia'
import { server } from '../../helpers/msw/server'
import { apiClient } from '@/api/client'

// vi.hoisted: vi.mock's factory below is hoisted above these imports/consts by vitest, so the
// fake connection must be defined via vi.hoisted to be visible inside the factory.
type Handlers = Record<string, (...args: unknown[]) => void>
const { makeFakeConnection, built, listeners, startMock, stopMock, withUrlMock } = vi.hoisted(
  () => {
    // Handlers of the most recently built connection — what fire() targets.
    const listeners: Handlers = {}
    // Every connection's own handlers, in build order, so a test can fire on a stale one.
    const built: Handlers[] = []
    const withUrlMock = vi.fn()
    const startMock = vi.fn(() => Promise.resolve())
    const stopMock = vi.fn(() => Promise.resolve())
    function makeFakeConnection() {
      for (const key of Object.keys(listeners)) delete listeners[key]
      const own: Handlers = {}
      built.push(own)
      const register = (event: string, handler: (...args: unknown[]) => void) => {
        own[event] = handler
        listeners[event] = handler
      }
      return {
        on: register,
        onreconnecting: (handler: () => void) => register('reconnecting', handler),
        onreconnected: (handler: () => void) => register('reconnected', handler),
        onclose: (handler: () => void) => register('close', handler),
        start: startMock,
        stop: stopMock,
      }
    }
    return { makeFakeConnection, built, listeners, startMock, stopMock, withUrlMock }
  },
)

// Throws instead of silently no-op'ing when the store never registered the handler — a missing
// registration is exactly the regression these tests exist to catch.
function fire(event: string, ...args: unknown[]) {
  const handler = listeners[event]
  if (!handler) throw new Error(`No SignalR handler registered for '${event}'`)
  handler(...args)
}

vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: class {
    withUrl(url: string) {
      withUrlMock(url)
      return this
    }
    withAutomaticReconnect() {
      return this
    }
    build() {
      return makeFakeConnection()
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
    expect(result.items[0]?.id).toBe('notif-1')
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

      fire('ReceiveNotification', makeNotification())

      expect(store.unreadCount).toBe(1)

      await store.disconnectRealtime()
    })

    it('resumes the poll fallback immediately on onreconnecting, before onclose fires', async () => {
      server.use(
        http.get('*/api/notifications/unread-count', () => HttpResponse.json({ count: 0 })),
      )
      const store = useNotificationsStore()
      store.startPolling()
      await store.connectRealtime()

      const hits = vi.fn()
      server.use(
        http.get('*/api/notifications/unread-count', () => {
          hits()
          return HttpResponse.json({ count: 0 })
        }),
      )

      fire('reconnecting')
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
      store.startPolling()
      await store.connectRealtime()
      fire('reconnecting')
      await flushHttp()

      const hits = vi.fn()
      server.use(
        http.get('*/api/notifications/unread-count', () => {
          hits()
          return HttpResponse.json({ count: 7 })
        }),
      )
      fire('reconnected')
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
      store.startPolling()
      await store.connectRealtime()

      const hits = vi.fn()
      server.use(
        http.get('*/api/notifications/unread-count', () => {
          hits()
          return HttpResponse.json({ count: 0 })
        }),
      )

      fire('close')
      await flushHttp()
      expect(hits).toHaveBeenCalledTimes(1)

      await vi.advanceTimersByTimeAsync(30_000)
      await flushHttp()
      expect(hits).toHaveBeenCalledTimes(2)
    })

    it('does not revive the poll when onclose fires after the app shell tore everything down', async () => {
      server.use(
        http.get('*/api/notifications/unread-count', () => HttpResponse.json({ count: 0 })),
      )
      const store = useNotificationsStore()
      store.startPolling()
      await store.connectRealtime()
      await flushHttp()

      const hits = vi.fn()
      server.use(
        http.get('*/api/notifications/unread-count', () => {
          hits()
          return HttpResponse.json({ count: 0 })
        }),
      )

      // AppLayout's onUnmounted order; a real connection.stop() then fires onclose.
      store.stopPolling()
      await store.disconnectRealtime()
      fire('close')
      fire('reconnecting')
      await flushHttp()
      await vi.advanceTimersByTimeAsync(30_000)
      await flushHttp()

      expect(hits).not.toHaveBeenCalled()
    })

    it('does not poll from realtime callbacks when polling was never started', async () => {
      const hits = vi.fn()
      server.use(
        http.get('*/api/notifications/unread-count', () => {
          hits()
          return HttpResponse.json({ count: 0 })
        }),
      )
      const store = useNotificationsStore()
      await store.connectRealtime()

      fire('close')
      await vi.advanceTimersByTimeAsync(30_000)
      await flushHttp()

      expect(hits).not.toHaveBeenCalled()
      await store.disconnectRealtime()
    })

    it('ignores a stale connection after a quick unmount/remount (logout then re-login)', async () => {
      server.use(
        http.get('*/api/notifications/unread-count', () => HttpResponse.json({ count: 0 })),
      )
      const store = useNotificationsStore()
      store.startPolling()
      await store.connectRealtime()
      const stale = built.at(-1)!

      // AppLayout's onUnmounted does not await disconnectRealtime; the remount runs first.
      store.stopPolling()
      const pendingDisconnect = store.disconnectRealtime()
      store.startPolling()
      await store.connectRealtime()
      await pendingDisconnect
      await flushHttp()

      const hits = vi.fn()
      server.use(
        http.get('*/api/notifications/unread-count', () => {
          hits()
          return HttpResponse.json({ count: 0 })
        }),
      )
      // The old connection's real stop() fires its onclose — it must not touch the new session.
      stale.close?.()
      await vi.advanceTimersByTimeAsync(30_000)
      await flushHttp()
      expect(hits).not.toHaveBeenCalled()

      // And the new connection must still be the one a later teardown stops.
      stopMock.mockClear()
      store.stopPolling()
      await store.disconnectRealtime()
      expect(stopMock).toHaveBeenCalledTimes(1)
    })

    it('builds the hub URL from the runtime API base URL without a double slash', async () => {
      const original = apiClient.defaults.baseURL
      apiClient.defaults.baseURL = 'https://api.example.test/'
      withUrlMock.mockClear()
      try {
        const store = useNotificationsStore()
        await store.connectRealtime()

        expect(withUrlMock).toHaveBeenCalledWith('https://api.example.test/hubs/notifications')
        await store.disconnectRealtime()
      } finally {
        apiClient.defaults.baseURL = original
      }
    })

    it('disconnectRealtime stops the hub connection', async () => {
      const store = useNotificationsStore()
      await store.connectRealtime()

      await store.disconnectRealtime()

      expect(stopMock).toHaveBeenCalledTimes(1)
    })
  })
})
