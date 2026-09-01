import { getHealth } from '@/api/generated/health/health'
import { isServerOutage } from '@/lib/api-outage'

const POLL_INTERVAL_MS = 60_000
// While the API looks down, re-check faster so the error screen clears within
// seconds of recovery instead of waiting up to a full POLL_INTERVAL_MS.
const RETRY_INTERVAL_MS = 10_000
// One failed poll can be a single dropped request or a momentary blip — only
// take over the whole screen once two checks in a row fail.
const FAILURE_THRESHOLD = 2

export type ApiHealthStatus = 'checking' | 'online' | 'offline' | 'error'

export const useHealthStore = defineStore('health', () => {
  const status = ref<ApiHealthStatus>('checking')
  const apiVersion = ref<string | null>(null)
  const apiBuildTimestamp = ref<string | null>(null)

  const isDown = computed(() => status.value === 'error')

  const client = getHealth()
  let timer: ReturnType<typeof setInterval> | null = null
  let currentInterval = POLL_INTERVAL_MS
  let consecutiveFailures = 0

  // Re-evaluate the poll cadence after every check: setInterval only, so the
  // fake-timer test setup (`toFake: ['setInterval', 'clearInterval']`, which MSW
  // needs to keep real setTimeout) can still drive it.
  // Switch to the fast cadence as soon as ANY check fails, not just once we've
  // escalated — so the confirming second failure (and the takeover) lands
  // ~RETRY_INTERVAL_MS after the first, not a full POLL_INTERVAL_MS later.
  function applyCadence() {
    if (!timer) return
    const desired = consecutiveFailures > 0 ? RETRY_INTERVAL_MS : POLL_INTERVAL_MS
    if (desired === currentInterval) return
    clearInterval(timer)
    currentInterval = desired
    timer = setInterval(check, currentInterval)
  }

  async function check() {
    try {
      const health = await client.healthGet()
      apiVersion.value = health.version
      apiBuildTimestamp.value = health.buildTimestamp
      consecutiveFailures = 0
      status.value = health.status === 'healthy' ? 'online' : 'offline'
    } catch (error) {
      if (!isServerOutage(error)) {
        consecutiveFailures = 0
        status.value = 'offline'
      } else {
        consecutiveFailures += 1
        status.value = consecutiveFailures >= FAILURE_THRESHOLD ? 'error' : 'offline'
      }
    }

    applyCadence()
  }

  // Called when another caller has already proven the API is unreachable (a
  // failed silent-refresh on app boot) — escalate straight to the takeover
  // instead of waiting for this store's own poll to rack up FAILURE_THRESHOLD.
  // Seeds consecutiveFailures so the next real check keeps or clears 'error'
  // consistently, and the fast recovery cadence applies once polling starts.
  function reportOutage() {
    consecutiveFailures = Math.max(consecutiveFailures, FAILURE_THRESHOLD)
    status.value = 'error'
    applyCadence()
  }

  function startPolling() {
    if (timer) return
    currentInterval = consecutiveFailures > 0 ? RETRY_INTERVAL_MS : POLL_INTERVAL_MS
    timer = setInterval(check, currentInterval)
    check()
  }

  function stopPolling() {
    if (timer) clearInterval(timer)
    timer = null
    currentInterval = POLL_INTERVAL_MS
    consecutiveFailures = 0
  }

  return {
    status,
    apiVersion,
    apiBuildTimestamp,
    isDown,
    check,
    reportOutage,
    startPolling,
    stopPolling,
  }
})
