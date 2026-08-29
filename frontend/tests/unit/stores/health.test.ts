import { http, HttpResponse } from 'msw'
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { useHealthStore } from '@/stores/health'
import { server } from '../../helpers/msw/server'
import { setupTestPinia } from '../../helpers/pinia'

function healthy(version = '2.0.0', buildTimestamp: string | null = '2026-01-01T00:00:00Z') {
  return HttpResponse.json({
    status: 'healthy',
    version,
    timestamp: '2026-06-01T10:00:00Z',
    buildTimestamp,
  })
}

function unhealthy(status = 'degraded') {
  return HttpResponse.json({
    status,
    version: '1.0.0',
    timestamp: '2026-06-01T10:00:00Z',
    buildTimestamp: null,
  })
}

// setInterval/clearInterval are faked but setTimeout stays real, so a real
// macrotask tick lets MSW's fetch response actually resolve.
function flushHttp() {
  return new Promise((resolve) => setTimeout(resolve, 0))
}

describe('useHealthStore', () => {
  beforeEach(() => {
    setupTestPinia()
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] })
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('starts in "checking" before the first poll resolves', async () => {
    server.use(http.get('*/api/health', () => new Promise<Response>(() => {})))
    const store = useHealthStore()

    store.startPolling()

    expect(store.status).toBe('checking')
    expect(store.isDown).toBe(false)
    store.stopPolling()
  })

  it('goes "online" and captures version/build when healthy', async () => {
    server.use(http.get('*/api/health', () => healthy('3.1.4', '2026-02-02T00:00:00Z')))
    const store = useHealthStore()

    store.startPolling()
    await flushHttp()

    expect(store.status).toBe('online')
    expect(store.apiVersion).toBe('3.1.4')
    expect(store.apiBuildTimestamp).toBe('2026-02-02T00:00:00Z')
    expect(store.isDown).toBe(false)
    store.stopPolling()
  })

  it('goes "offline" (not "error") when the body reports a non-healthy status', async () => {
    server.use(http.get('*/api/health', () => unhealthy()))
    const store = useHealthStore()

    store.startPolling()
    await flushHttp()

    expect(store.status).toBe('offline')
    expect(store.isDown).toBe(false)
    store.stopPolling()
  })

  it('stays "offline" on a single 5xx, escalates to "error" only after two in a row', async () => {
    server.use(http.get('*/api/health', () => new HttpResponse(null, { status: 503 })))
    const store = useHealthStore()

    store.startPolling()
    await flushHttp()
    expect(store.status).toBe('offline')
    expect(store.isDown).toBe(false)

    // After the first failure the cadence drops to RETRY_INTERVAL_MS, so the
    // confirming second poll (and the takeover) lands ~10s later, not ~60s.
    await vi.advanceTimersByTimeAsync(10_000)
    await flushHttp()
    expect(store.status).toBe('error')
    expect(store.isDown).toBe(true)
    store.stopPolling()
  })

  it('escalates to "error" on repeated unreachable-host failures (no response)', async () => {
    server.use(http.get('*/api/health', () => HttpResponse.error()))
    const store = useHealthStore()

    store.startPolling()
    await flushHttp()
    await vi.advanceTimersByTimeAsync(10_000)
    await flushHttp()

    expect(store.status).toBe('error')
    store.stopPolling()
  })

  it('never escalates on a 4xx — treats it as "offline"', async () => {
    server.use(http.get('*/api/health', () => new HttpResponse(null, { status: 403 })))
    const store = useHealthStore()

    store.startPolling()
    await flushHttp()
    await vi.advanceTimersByTimeAsync(60_000)
    await flushHttp()
    await vi.advanceTimersByTimeAsync(60_000)
    await flushHttp()

    expect(store.status).toBe('offline')
    expect(store.isDown).toBe(false)
    store.stopPolling()
  })

  it('re-checks quickly while down and clears the error on recovery', async () => {
    let down = true
    server.use(
      http.get('*/api/health', () => (down ? new HttpResponse(null, { status: 500 }) : healthy())),
    )
    const store = useHealthStore()

    store.startPolling()
    await flushHttp()
    await vi.advanceTimersByTimeAsync(10_000)
    await flushHttp()
    expect(store.status).toBe('error')

    down = false
    // Recovery poll fires 10s after entering "error", not a full 60s later.
    await vi.advanceTimersByTimeAsync(10_000)
    await flushHttp()
    expect(store.status).toBe('online')
    expect(store.isDown).toBe(false)
    store.stopPolling()
  })

  it('check() re-polls immediately (the Retry button) and clears the error on success', async () => {
    let down = true
    server.use(
      http.get('*/api/health', () => (down ? new HttpResponse(null, { status: 500 }) : healthy())),
    )
    const store = useHealthStore()

    store.startPolling()
    await flushHttp()
    await vi.advanceTimersByTimeAsync(10_000)
    await flushHttp()
    expect(store.status).toBe('error')

    down = false
    await store.check()
    expect(store.status).toBe('online')
    store.stopPolling()
  })

  it('reportOutage() escalates straight to "error" without a request', async () => {
    const hits = vi.fn()
    server.use(
      http.get('*/api/health', () => {
        hits()
        return healthy()
      }),
    )
    const store = useHealthStore()

    store.reportOutage()

    expect(store.status).toBe('error')
    expect(store.isDown).toBe(true)
    expect(hits).not.toHaveBeenCalled()
  })

  it('recovers to "online" on the next poll after reportOutage() (fast cadence)', async () => {
    let down = true
    server.use(
      http.get('*/api/health', () => (down ? new HttpResponse(null, { status: 500 }) : healthy())),
    )
    const store = useHealthStore()

    store.reportOutage()
    store.startPolling()
    await flushHttp()
    // First poll after boot still fails → stays down.
    expect(store.status).toBe('error')

    down = false
    await vi.advanceTimersByTimeAsync(10_000)
    await flushHttp()
    expect(store.status).toBe('online')
    expect(store.isDown).toBe(false)
    store.stopPolling()
  })

  it('stops polling on stopPolling', async () => {
    const hits = vi.fn()
    server.use(
      http.get('*/api/health', () => {
        hits()
        return healthy()
      }),
    )
    const store = useHealthStore()

    store.startPolling()
    await flushHttp()
    expect(hits).toHaveBeenCalledTimes(1)

    store.stopPolling()
    await vi.advanceTimersByTimeAsync(120_000)
    await flushHttp()
    expect(hits).toHaveBeenCalledTimes(1)
  })
})
