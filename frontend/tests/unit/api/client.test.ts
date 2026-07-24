import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/stores/auth'
import { KEEP_LOGIN_STORAGE_KEY, setKeepLoginPreference } from '@/lib/auth-session'
import { server } from '../../helpers/msw/server'
import { makeAuthResponse } from '../../helpers/msw/fixtures'
import { setupTestPinia } from '../../helpers/pinia'

function isApiError(error: unknown): error is { name: string; status: number } {
  return (
    typeof error === 'object' &&
    error !== null &&
    (error as { name?: string }).name === 'ApiError' &&
    typeof (error as { status?: number }).status === 'number'
  )
}

async function loadApiClient() {
  const { apiClient } = await import('@/api/client')
  return apiClient
}

function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((res) => {
    resolve = res
  })
  return { promise, resolve }
}

describe('apiClient interceptors', () => {
  beforeEach(() => {
    vi.resetModules()
    setupTestPinia()
  })

  it('returns successful responses', async () => {
    server.use(http.get('*/api/test-protected', () => HttpResponse.json({ ok: true })))

    const apiClient = await loadApiClient()
    const response = await apiClient.get('/api/test-protected')

    expect(response.data).toEqual({ ok: true })
  })

  it('refreshes on 401 then retries the original request', async () => {
    let protectedCalls = 0

    server.use(
      http.get('*/api/test-protected', () => {
        protectedCalls++
        if (protectedCalls === 1) {
          return HttpResponse.json(null, { status: 401 })
        }
        return HttpResponse.json({ ok: true })
      }),
      http.post('*/api/auth/refresh', () => HttpResponse.json(makeAuthResponse())),
    )

    const apiClient = await loadApiClient()
    const response = await apiClient.get('/api/test-protected')
    const auth = useAuthStore()

    expect(response.data).toEqual({ ok: true })
    expect(protectedCalls).toBe(2)
    expect(auth.account?.username).toBe('test')
  })

  it('queues a second concurrent 401 instead of triggering a second refresh', async () => {
    let protectedCalls = 0
    let refreshCalls = 0
    const refreshGate = deferred<void>()

    server.use(
      http.get('*/api/test-protected', () => {
        protectedCalls++
        // The first 2 calls are the two original (pre-refresh) requests;
        // everything after is a post-refresh retry.
        if (protectedCalls <= 2) {
          return HttpResponse.json(null, { status: 401 })
        }
        return HttpResponse.json({ ok: true })
      }),
      http.post('*/api/auth/refresh', async () => {
        refreshCalls++
        await refreshGate.promise
        return HttpResponse.json(makeAuthResponse())
      }),
    )

    const apiClient = await loadApiClient()

    const first = apiClient.get('/api/test-protected')
    // Wait until the first 401 has actually triggered the refresh call
    // (refreshClient.post is invoked synchronously before isRefreshing's
    // guard would let a second 401 through) before firing the second
    // request, so the second request deterministically hits the
    // isRefreshing branch instead of racing to start its own refresh.
    await vi.waitFor(() => {
      if (refreshCalls !== 1) throw new Error('refresh not started yet')
    })

    const second = apiClient.get('/api/test-protected')
    // Wait for request 2's ORIGINAL 401 round-trip to actually land before
    // letting the refresh resolve — axios always sends the request over the
    // wire regardless of isRefreshing; only once its 401 response comes
    // back does the interceptor check isRefreshing and enqueue it. If the
    // gate resolves too early, request 2's original and its post-refresh
    // retry can interleave unpredictably against the shared call counter.
    await vi.waitFor(() => {
      if (protectedCalls !== 2) throw new Error('second original 401 not received yet')
    })

    refreshGate.resolve()

    const [firstResponse, secondResponse] = await Promise.all([first, second])

    expect(refreshCalls).toBe(1)
    expect(firstResponse.data).toEqual({ ok: true })
    expect(secondResponse.data).toEqual({ ok: true })
  })

  it('rejects every queued request when the shared refresh fails', async () => {
    let protectedCalls = 0
    let refreshCalls = 0
    const refreshGate = deferred<void>()

    server.use(
      http.get('*/api/test-protected', () => {
        protectedCalls++
        return HttpResponse.json(null, { status: 401 })
      }),
      http.post('*/api/auth/refresh', async () => {
        refreshCalls++
        await refreshGate.promise
        return HttpResponse.json(null, { status: 401 })
      }),
    )

    const apiClient = await loadApiClient()
    const auth = useAuthStore()
    auth.setAuth(makeAuthResponse())

    const first = apiClient.get('/api/test-protected')
    await vi.waitFor(() => {
      if (refreshCalls !== 1) throw new Error('refresh not started yet')
    })

    const second = apiClient.get('/api/test-protected')
    // Same reasoning as the success-path test above: wait for request 2's
    // own 401 round-trip before letting the shared refresh (and thus both
    // requests' terminal rejection) proceed.
    await vi.waitFor(() => {
      if (protectedCalls !== 2) throw new Error('second original 401 not received yet')
    })

    refreshGate.resolve()

    await expect(first).rejects.toBeDefined()
    await expect(second).rejects.toBeDefined()
    expect(refreshCalls).toBe(1)
    expect(auth.account).toBeNull()
  })

  it('clears auth when refresh fails', async () => {
    server.use(
      http.get('*/api/test-protected', () => HttpResponse.json(null, { status: 401 })),
      http.post('*/api/auth/refresh', () => HttpResponse.json(null, { status: 401 })),
    )

    const apiClient = await loadApiClient()
    const auth = useAuthStore()
    auth.setAuth(makeAuthResponse())

    await expect(apiClient.get('/api/test-protected')).rejects.toBeDefined()
    expect(auth.account).toBeNull()
  })

  it('clears the keep-login preference when a mid-session refresh fails', async () => {
    server.use(
      http.get('*/api/test-protected', () => HttpResponse.json(null, { status: 401 })),
      http.post('*/api/auth/refresh', () => HttpResponse.json(null, { status: 401 })),
    )
    setKeepLoginPreference(true)

    const apiClient = await loadApiClient()
    const auth = useAuthStore()
    auth.setAuth(makeAuthResponse())

    await expect(apiClient.get('/api/test-protected')).rejects.toBeDefined()
    expect(localStorage.getItem(KEEP_LOGIN_STORAGE_KEY)).toBeNull()
  })

  it('does not refresh on auth flow 401', async () => {
    let refreshCalls = 0

    server.use(
      http.post('*/api/auth/login', () => HttpResponse.json(null, { status: 401 })),
      http.post('*/api/auth/refresh', () => {
        refreshCalls++
        return HttpResponse.json(makeAuthResponse())
      }),
    )

    const apiClient = await loadApiClient()

    await expect(apiClient.post('/api/auth/login', {})).rejects.toBeDefined()
    expect(refreshCalls).toBe(0)
  })

  it('does not refresh on password change 401', async () => {
    let refreshCalls = 0

    server.use(
      http.put('*/api/profile/password', () => HttpResponse.json(null, { status: 401 })),
      http.post('*/api/auth/refresh', () => {
        refreshCalls++
        return HttpResponse.json(makeAuthResponse())
      }),
    )

    const apiClient = await loadApiClient()

    await expect(
      apiClient.put('/api/profile/password', { currentPassword: 'a', newPassword: 'b' }),
    ).rejects.toBeDefined()
    expect(refreshCalls).toBe(0)
  })

  it('maps 429 responses to ApiError', async () => {
    server.use(
      http.get('*/api/test-protected', () =>
        HttpResponse.json(
          { status: 429, title: 'Too Many Requests' },
          {
            status: 429,
            headers: { 'Content-Type': 'application/problem+json' },
          },
        ),
      ),
    )

    const apiClient = await loadApiClient()

    try {
      await apiClient.get('/api/test-protected')
      expect.unreachable('expected request to fail')
    } catch (error) {
      expect(isApiError(error)).toBe(true)
      if (isApiError(error)) {
        expect(error.status).toBe(429)
      }
    }
  })
})
