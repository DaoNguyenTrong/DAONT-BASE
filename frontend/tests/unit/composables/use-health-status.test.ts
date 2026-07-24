import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { useHealthStatus } from '@/composables/use-health-status'
import { server } from '../../helpers/msw/server'
import { withSetup } from '../../helpers/with-setup'
import { setupTestPinia } from '../../helpers/pinia'

function healthy(version = '2.0.0', buildTimestamp: string | null = '2026-01-01T00:00:00Z') {
  return HttpResponse.json({
    status: 'healthy',
    version,
    timestamp: '2026-06-01T10:00:00Z',
    buildTimestamp,
  })
}

// setInterval/clearInterval are faked but setTimeout stays real, so a real
// macrotask tick lets MSW's fetch response actually resolve.
function flushHttp() {
  return new Promise((resolve) => setTimeout(resolve, 0))
}

describe('useHealthStatus', () => {
  beforeEach(() => {
    // apiClient's request interceptor reads useAuthStore(), which throws
    // without an active Pinia instance.
    setupTestPinia()
    // Only fake setInterval/clearInterval — faking setTimeout too breaks MSW's
    // node-http-based interceptor, which relies on real timers to deliver responses.
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] })
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('starts in "checking" state before the first check resolves', async () => {
    // Handler that never responds so the initial check stays pending.
    server.use(http.get('*/api/health', () => new Promise<Response>(() => {})))

    const { result, app } = await withSetup(() => useHealthStatus())

    expect(result.status.value).toBe('checking')
    expect(result.apiVersion.value).toBeNull()
    expect(result.apiBuildTimestamp.value).toBeNull()

    app.unmount()
  })

  it('sets status to "online" and populates version/build when healthy', async () => {
    server.use(http.get('*/api/health', () => healthy('3.1.4', '2026-02-02T00:00:00Z')))

    const { result, app } = await withSetup(() => useHealthStatus())
    await flushHttp()

    expect(result.status.value).toBe('online')
    expect(result.apiVersion.value).toBe('3.1.4')
    expect(result.apiBuildTimestamp.value).toBe('2026-02-02T00:00:00Z')

    app.unmount()
  })

  it('sets status to "offline" when the reported status is not "healthy"', async () => {
    server.use(
      http.get('*/api/health', () =>
        HttpResponse.json({
          status: 'degraded',
          version: '1.0.0',
          timestamp: '2026-06-01T10:00:00Z',
          buildTimestamp: null,
        }),
      ),
    )

    const { result, app } = await withSetup(() => useHealthStatus())
    await flushHttp()

    expect(result.status.value).toBe('offline')
    expect(result.apiVersion.value).toBe('1.0.0')

    app.unmount()
  })

  it('catches errors and sets status to "offline" without throwing', async () => {
    const unhandled = vi.fn()
    process.on('unhandledRejection', unhandled)
    server.use(http.get('*/api/health', () => new HttpResponse(null, { status: 500 })))

    const { result, app } = await withSetup(() => useHealthStatus())
    await flushHttp()

    expect(result.status.value).toBe('offline')
    expect(unhandled).not.toHaveBeenCalled()

    process.off('unhandledRejection', unhandled)
    app.unmount()
  })

  it('re-polls every 60s and updates status when the response changes', async () => {
    let ok = true
    server.use(
      http.get('*/api/health', () =>
        ok
          ? healthy()
          : HttpResponse.json({
              status: 'degraded',
              version: '2.0.0',
              timestamp: '2026-06-01T10:00:00Z',
              buildTimestamp: null,
            }),
      ),
    )

    const { result, app } = await withSetup(() => useHealthStatus())
    await flushHttp()
    expect(result.status.value).toBe('online')

    ok = false
    await vi.advanceTimersByTimeAsync(60_000)
    await flushHttp()
    expect(result.status.value).toBe('offline')

    app.unmount()
  })

  it('clears the interval on unmount so no further polls happen', async () => {
    const hits = vi.fn()
    server.use(
      http.get('*/api/health', () => {
        hits()
        return healthy()
      }),
    )

    const { app } = await withSetup(() => useHealthStatus())
    await flushHttp()
    expect(hits).toHaveBeenCalledTimes(1)

    app.unmount()
    await vi.advanceTimersByTimeAsync(60_000)
    await flushHttp()
    expect(hits).toHaveBeenCalledTimes(1)
  })
})
