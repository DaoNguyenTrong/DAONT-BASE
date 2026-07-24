import { getHealth } from '@/api/generated/health/health'

const POLL_INTERVAL_MS = 60_000

export type ApiHealthStatus = 'checking' | 'online' | 'offline'

export function useHealthStatus() {
  const status = ref<ApiHealthStatus>('checking')
  const apiVersion = ref<string | null>(null)
  const apiBuildTimestamp = ref<string | null>(null)
  const healthClient = getHealth()

  async function check() {
    try {
      const health = await healthClient.healthGet()
      apiVersion.value = health.version
      apiBuildTimestamp.value = health.buildTimestamp
      status.value = health.status === 'healthy' ? 'online' : 'offline'
    } catch {
      status.value = 'offline'
    }
  }

  onMounted(() => {
    check()
    const timer = setInterval(check, POLL_INTERVAL_MS)
    onUnmounted(() => clearInterval(timer))
  })

  return { status, apiVersion, apiBuildTimestamp, check }
}
