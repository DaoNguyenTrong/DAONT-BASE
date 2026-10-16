/**
 * The one gate every `VITE_*` value passes through — no other file reads `import.meta.env.VITE_*`.
 * `getRuntimeConfig()` overlays the deploy-time `/config.json` (see `public/config.example.json`),
 * fetched once by `loadRuntimeConfig()` in `main.ts`'s `bootstrap()`, over the build-time `VITE_*`
 * default, key by key. One built image can therefore enable OAuth/push or point at another backend
 * per environment without a rebuild; local dev keeps working from `.env` alone.
 *
 * Never call `getRuntimeConfig()` at a plain `.ts` module's top level: that freezes the value at
 * first import, possibly before `loadRuntimeConfig()` resolved. Call it inside a function body —
 * a composable, a store action, or `<script setup>` (per-instance setup code, so always safe).
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
 * Fetches `/config.json` (a static file, not processed by Vite) and keeps every recognized key whose
 * value is a non-blank string. Never throws: a missing, unreachable or malformed file — or a field
 * that fails validation — just means the build-time default applies for that key. Call once at boot,
 * before any value is read (see `main.ts`).
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
