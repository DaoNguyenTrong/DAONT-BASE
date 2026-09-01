import { ApiError } from '@/api/types'

/**
 * True when an error means the API itself is unreachable or broken — a 5xx from
 * the API (or the proxy in front of it), or no response at all (network / DNS /
 * connection refused).
 *
 * A 4xx is explicitly NOT an outage: the server is up and answering, so it stays
 * a normal error (e.g. a 401 with no valid session must still route to /login,
 * not to the full-screen ServerErrorScreen).
 */
export function isServerOutage(error: unknown): boolean {
  const status =
    error instanceof ApiError
      ? error.status
      : (error as { response?: { status?: number } })?.response?.status

  return typeof status !== 'number' || status >= 500
}
