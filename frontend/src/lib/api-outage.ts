import { ApiError } from '@/api/types'

/**
 * True when the API is unreachable or broken: a 5xx (from the API or its proxy) or no response.
 * A 4xx is never an outage — e.g. a 401 must still route to /login, not to ServerErrorScreen.
 */
export function isServerOutage(error: unknown): boolean {
  const status =
    error instanceof ApiError
      ? error.status
      : (error as { response?: { status?: number } })?.response?.status

  return typeof status !== 'number' || status >= 500
}
