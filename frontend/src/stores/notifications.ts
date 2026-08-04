import { getNotifications } from '@/api/generated/notifications/notifications'
import { toPagedResult } from '@/lib/paged-result'
import type { Notification } from '@/api/types'

const POLL_INTERVAL_MS = 30_000

export const useNotificationsStore = defineStore('notifications', () => {
  const unreadCount = ref(0)
  const client = getNotifications()
  let intervalHandle: ReturnType<typeof setInterval> | null = null

  async function fetchUnreadCount() {
    const response = await client.notificationsGetUnreadCount()
    unreadCount.value = Number(response.count)
  }

  async function fetchList(pageNumber: number, pageSize: number) {
    const response = await client.notificationsGetAll({ pageNumber, pageSize })
    return toPagedResult<Notification>(response)
  }

  async function markAsRead(id: string) {
    await client.notificationsMarkAsRead(id)
    await fetchUnreadCount()
  }

  async function markAllAsRead() {
    await client.notificationsMarkAllAsRead()
    await fetchUnreadCount()
  }

  function resume() {
    fetchUnreadCount()
    intervalHandle ??= setInterval(fetchUnreadCount, POLL_INTERVAL_MS)
  }

  function pause() {
    if (intervalHandle) clearInterval(intervalHandle)
    intervalHandle = null
  }

  function handleVisibilityChange() {
    if (document.visibilityState === 'visible') resume()
    else pause()
  }

  function startPolling() {
    document.addEventListener('visibilitychange', handleVisibilityChange)
    if (document.visibilityState === 'visible') resume()
  }

  function stopPolling() {
    pause()
    document.removeEventListener('visibilitychange', handleVisibilityChange)
  }

  return {
    unreadCount,
    fetchUnreadCount,
    fetchList,
    markAsRead,
    markAllAsRead,
    startPolling,
    stopPolling,
  }
})
