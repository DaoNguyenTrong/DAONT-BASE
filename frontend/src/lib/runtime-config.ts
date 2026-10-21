/**
 * The only reader of `import.meta.env.VITE_*`; deploy-time `/config.json` overrides each key, so
 * one image serves every environment. Never call `getRuntimeConfig()` at a module's top level: it
 * can freeze the value before `loadRuntimeConfig()` resolves. Call it inside a function body.
 */

export interface RuntimeConfig {
  apiBaseUrl?: string
  googleClientId?: string
  microsoftClientId?: string
  microsoftTenantId?: string
  firebaseApiKey?: string
  firebaseAuthDomain?: string
  firebaseProjectId?: string
  firebaseMessagingSenderId?: string
  firebaseAppId?: string
  firebaseVapidKey?: string
}

export type ResolvedRuntimeConfig = Required<RuntimeConfig>

const RUNTIME_CONFIG_KEYS = [
  'apiBaseUrl',
  'googleClientId',
  'microsoftClientId',
  'microsoftTenantId',
  'firebaseApiKey',
  'firebaseAuthDomain',
  'firebaseProjectId',
  'firebaseMessagingSenderId',
  'firebaseAppId',
  'firebaseVapidKey',
] as const satisfies readonly (keyof RuntimeConfig)[]

// Read on every call, not snapshotted: `vi.stubEnv` in tests, and any module regardless of when it
// first imported this file, then always see the current `import.meta.env`.
function readBuildTimeDefaults(): ResolvedRuntimeConfig {
  return {
    apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? '',
    googleClientId: import.meta.env.VITE_GOOGLE_CLIENT_ID ?? '',
    microsoftClientId: import.meta.env.VITE_MICROSOFT_CLIENT_ID ?? '',
    microsoftTenantId: import.meta.env.VITE_MICROSOFT_TENANT_ID || 'common',
    firebaseApiKey: import.meta.env.VITE_FIREBASE_API_KEY ?? '',
    firebaseAuthDomain: import.meta.env.VITE_FIREBASE_AUTH_DOMAIN ?? '',
    firebaseProjectId: import.meta.env.VITE_FIREBASE_PROJECT_ID ?? '',
    firebaseMessagingSenderId: import.meta.env.VITE_FIREBASE_MESSAGING_SENDER_ID ?? '',
    firebaseAppId: import.meta.env.VITE_FIREBASE_APP_ID ?? '',
    firebaseVapidKey: import.meta.env.VITE_FIREBASE_VAPID_KEY ?? '',
  }
}

let overrides: RuntimeConfig = {}

/**
 * Keeps each recognized `/config.json` key with a non-blank string value. Never throws: a missing
 * or malformed file or field falls back to the build-time default. Call once at boot, before reads.
 */
export async function loadRuntimeConfig(): Promise<RuntimeConfig> {
  overrides = await fetchOverrides()
  return overrides
}

export function getRuntimeConfig(): ResolvedRuntimeConfig {
  return { ...readBuildTimeDefaults(), ...overrides }
}

async function fetchOverrides(): Promise<RuntimeConfig> {
  try {
    const response = await fetch('/config.json', { cache: 'no-store' })

    if (!response.ok) {
      return {}
    }

    const data: unknown = await response.json()

    if (typeof data !== 'object' || data === null) {
      return {}
    }

    const result: RuntimeConfig = {}
    for (const key of RUNTIME_CONFIG_KEYS) {
      const value = (data as Record<string, unknown>)[key]
      if (typeof value === 'string' && value.trim().length > 0) {
        result[key] = value
      }
    }

    return result
  } catch {
    return {}
  }
}
