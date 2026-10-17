import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { ApiError } from '@/api/types'
import { apiRequest } from '@/api/mutator'
import { server } from '../../helpers/msw/server'
import { setupTestPinia } from '../../helpers/pinia'

describe('apiRequest', () => {
  beforeEach(() => {
    // The client's error interceptor reads the auth store before checking the status.
    setupTestPinia()
  })

  it('resolves with the response body rather than the axios response', async () => {
    server.use(http.get('*/api/test-mutator', () => HttpResponse.json({ ok: true, count: 2 })))

    const data = await apiRequest<{ ok: boolean; count: number }>({
      url: '/api/test-mutator',
      method: 'GET',
    })

    expect(data).toEqual({ ok: true, count: 2 })
  })

  it('forwards the method, query params and JSON body to the server', async () => {
    let received: { method: string; search: string; body: unknown } | undefined
    server.use(
      http.post('*/api/test-mutator', async ({ request }) => {
        received = {
          method: request.method,
          search: new URL(request.url).search,
          body: await request.json(),
        }
        return HttpResponse.json({ id: 'new' }, { status: 201 })
      }),
    )

    const data = await apiRequest<{ id: string }>({
      url: '/api/test-mutator',
      method: 'POST',
      params: { page: 2, ids: [3, 4] },
      data: { name: 'Acme' },
    })

    expect(data).toEqual({ id: 'new' })
    expect(received).toEqual({
      method: 'POST',
      search: '?page=2&ids=3&ids=4',
      body: { name: 'Acme' },
    })
  })

  it('resolves with the empty-string body axios gives a 204 response', async () => {
    server.use(http.delete('*/api/test-mutator', () => new HttpResponse(null, { status: 204 })))

    const data = await apiRequest<void>({ url: '/api/test-mutator', method: 'DELETE' })

    expect(data).toBe('')
  })

  it.each([400, 404, 422, 500])(
    'rejects with an ApiError carrying the problem details for a %i problem response',
    async (status) => {
      server.use(
        http.get('*/api/test-mutator', () =>
          HttpResponse.json(
            { status, title: 'Problem', detail: 'Something went wrong', code: 'SomeCode' },
            { status, headers: { 'Content-Type': 'application/problem+json' } },
          ),
        ),
      )

      const error = await apiRequest({ url: '/api/test-mutator', method: 'GET' }).catch(
        (e: unknown) => e,
      )

      expect(error).toBeInstanceOf(ApiError)
      expect(error).toMatchObject({
        status,
        message: 'Something went wrong',
        problem: { code: 'SomeCode' },
      })
    },
  )

  it('rejects with a non-ApiError when the request never gets a response', async () => {
    server.use(http.get('*/api/test-mutator', () => HttpResponse.error()))

    const error = await apiRequest({ url: '/api/test-mutator', method: 'GET' }).catch(
      (e: unknown) => e,
    )

    expect(error).toBeInstanceOf(Error)
    expect(error).not.toBeInstanceOf(ApiError)
  })
})
