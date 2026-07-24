import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { loadRuntimeConfig } from '@/lib/runtime-config'
import { server } from '../../helpers/msw/server'

describe('loadRuntimeConfig', () => {
  it('returns the apiBaseUrl from config.json when present and valid', async () => {
    server.use(
      http.get('*/config.json', () => HttpResponse.json({ apiBaseUrl: 'https://api.example.com' })),
    )

    const config = await loadRuntimeConfig()

    expect(config).toEqual({ apiBaseUrl: 'https://api.example.com' })
  })

  it('returns an empty object when config.json is missing (404)', async () => {
    server.use(http.get('*/config.json', () => new HttpResponse(null, { status: 404 })))

    const config = await loadRuntimeConfig()

    expect(config).toEqual({})
  })

  it('returns an empty object when apiBaseUrl is missing from the response', async () => {
    server.use(http.get('*/config.json', () => HttpResponse.json({})))

    const config = await loadRuntimeConfig()

    expect(config).toEqual({})
  })

  it('returns an empty object when apiBaseUrl is blank', async () => {
    server.use(http.get('*/config.json', () => HttpResponse.json({ apiBaseUrl: '   ' })))

    const config = await loadRuntimeConfig()

    expect(config).toEqual({})
  })

  it('returns an empty object when apiBaseUrl is not a string', async () => {
    server.use(http.get('*/config.json', () => HttpResponse.json({ apiBaseUrl: 123 })))

    const config = await loadRuntimeConfig()

    expect(config).toEqual({})
  })

  it('returns an empty object when the response is not valid JSON', async () => {
    server.use(
      http.get(
        '*/config.json',
        () => new HttpResponse('not json', { headers: { 'Content-Type': 'application/json' } }),
      ),
    )

    const config = await loadRuntimeConfig()

    expect(config).toEqual({})
  })

  it('returns an empty object on a network error', async () => {
    server.use(http.get('*/config.json', () => HttpResponse.error()))

    const config = await loadRuntimeConfig()

    expect(config).toEqual({})
  })
})
