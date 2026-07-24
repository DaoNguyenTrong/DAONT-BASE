import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/stores/auth'
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

describe('apiClient interceptors', () => {
  beforeEach(() => {
    vi.resetModules()
    setupTestPinia()
  })

  it('returns successful responses', async () => {
    server.use(
      http.get('*/api/test-protected', () => HttpResponse.json({ ok: true })),
    )

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
