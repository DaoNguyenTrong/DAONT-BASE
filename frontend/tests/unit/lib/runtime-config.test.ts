import { http, HttpResponse } from 'msw'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { getRuntimeConfig, loadRuntimeConfig } from '@/lib/runtime-config'
import { server } from '../../helpers/msw/server'

// loadRuntimeConfig() keeps the overrides for the page's lifetime — reset them between tests.
afterEach(async () => {
  vi.unstubAllEnvs()
  server.use(http.get('*/config.json', () => new HttpResponse(null, { status: 404 })))
  await loadRuntimeConfig()
})

describe('loadRuntimeConfig', () => {
  it('returns the apiBaseUrl from config.json when present and valid', async () => {
    server.use(
      http.get('*/config.json', () => HttpResponse.json({ apiBaseUrl: 'https://api.example.com' })),
    )

    const config = await loadRuntimeConfig()

    expect(config).toEqual({ apiBaseUrl: 'https://api.example.com' })
  })

  it.each([
    [
      'returns an empty object when config.json is missing (404)',
      () => new HttpResponse(null, { status: 404 }),
    ],
    [
      'returns an empty object when apiBaseUrl is missing from the response',
      () => HttpResponse.json({}),
    ],
    [
      'returns an empty object when apiBaseUrl is blank',
      () => HttpResponse.json({ apiBaseUrl: '   ' }),
    ],
    [
      'returns an empty object when apiBaseUrl is not a string',
      () => HttpResponse.json({ apiBaseUrl: 123 }),
    ],
  ])('%s', async (_title, respond) => {
    server.use(http.get('*/config.json', respond))

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

describe('getRuntimeConfig', () => {
  it('falls back to the build-time VITE_* values when config.json is absent', async () => {
    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', 'build-google')
    vi.stubEnv('VITE_MICROSOFT_TENANT_ID', '')
    server.use(http.get('*/config.json', () => new HttpResponse(null, { status: 404 })))

    await loadRuntimeConfig()

    expect(getRuntimeConfig().googleClientId).toBe('build-google')
    // Blank tenant falls back to the multi-tenant default, same as the backend.
    expect(getRuntimeConfig().microsoftTenantId).toBe('common')
  })

  it('overlays config.json key by key over the build-time values', async () => {
    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', 'build-google')
    vi.stubEnv('VITE_MICROSOFT_CLIENT_ID', 'build-microsoft')
    server.use(
      http.get('*/config.json', () =>
        HttpResponse.json({
          googleClientId: 'runtime-google',
          microsoftClientId: '  ',
          firebaseVapidKey: 'runtime-vapid',
          somethingUnknown: 'ignored',
        }),
      ),
    )

    await loadRuntimeConfig()
    const config = getRuntimeConfig()

    expect(config.googleClientId).toBe('runtime-google')
    // A blank override doesn't erase the build-time value.
    expect(config.microsoftClientId).toBe('build-microsoft')
    expect(config.firebaseVapidKey).toBe('runtime-vapid')
    expect(config).not.toHaveProperty('somethingUnknown')
  })

  it('reads the build-time values fresh on every call', () => {
    vi.stubEnv('VITE_FIREBASE_API_KEY', 'first')
    expect(getRuntimeConfig().firebaseApiKey).toBe('first')

    vi.stubEnv('VITE_FIREBASE_API_KEY', 'second')
    expect(getRuntimeConfig().firebaseApiKey).toBe('second')
  })
})
