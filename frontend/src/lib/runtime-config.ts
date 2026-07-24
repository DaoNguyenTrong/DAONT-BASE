export interface RuntimeConfig {
  apiBaseUrl?: string
}

/**
 * Fetches `/config.json` (a plain static file, not processed by Vite — see
 * `public/config.example.json`) so a single built bundle can be pointed at a
 * different backend per deploy environment without a rebuild. Returns an
 * empty object — never throws — when the file is missing, unreachable, or
 * malformed, so callers fall back to the build-time `VITE_API_BASE_URL`.
 */
export async function loadRuntimeConfig(): Promise<RuntimeConfig> {
  try {
    const response = await fetch('/config.json', { cache: 'no-store' })

    if (!response.ok) {
      return {}
    }

    const data: unknown = await response.json()
    const apiBaseUrl =
      typeof data === 'object' && data !== null
        ? (data as Record<string, unknown>).apiBaseUrl
        : undefined

    if (typeof apiBaseUrl === 'string' && apiBaseUrl.trim().length > 0) {
      return { apiBaseUrl }
    }

    return {}
  } catch {
    return {}
  }
}
