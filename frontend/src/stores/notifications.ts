import * as signalR from '@microsoft/signalr'
import { getNotifications } from '@/api/generated/notifications/notifications'
import { toPagedResult } from '@/lib/paged-result'
import type { Notification } from '@/api/types'

const POLL_INTERVAL_MS = 30_000

export const useNotificationsStore = defineStore('notifications', () => {
  const unreadCount = ref(0)
  const client = getNotifications()
  let intervalHandle: ReturnType<typeof setInterval> | null = null
  let connection: signalR.HubConnection | null = null
  let isRealtimeConnected = false

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
    // Không schedule interval khi SignalR đang sống — poll chỉ là fallback, tránh double-fetch
    // dư thừa trong lúc kênh realtime đã đang giao hàng (xem connectRealtime()).
    if (!isRealtimeConnected) {
      intervalHandle ??= setInterval(fetchUnreadCount, POLL_INTERVAL_MS)
    }
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

  async function connectRealtime() {
    connection = new signalR.HubConnectionBuilder()
      .withUrl(`${import.meta.env.VITE_API_BASE_URL}/hubs/notifications`, { withCredentials: true })
      .withAutomaticReconnect()
      .build()

    connection.on('ReceiveNotification', (_dto: Notification) => {
      unreadCount.value += 1
    })

    connection.onreconnecting(() => {
      // Đang trong cửa sổ retry của withAutomaticReconnect (backoff mặc định tới ~30-60s) —
      // CHƯA phải onclose, nhưng kết nối đã mất. Phải resume poll ngay ở đây, không đợi
      // onclose, nếu không sẽ có khoảng trống không kênh nào đang giao hàng.
      isRealtimeConnected = false
      resume()
    })

    connection.onreconnected(() => {
      isRealtimeConnected = true
      pause() // dừng poll trở lại khi WS sống lại
      fetchUnreadCount() // catch-up: bù các notification có thể miss lúc mất kết nối
    })

    connection.onclose(() => {
      isRealtimeConnected = false
      resume() // fallback: SignalR hết lượt tự reconnect (hoặc chưa từng start được), quay lại poll
    })

    try {
      await connection.start()
      isRealtimeConnected = true
      pause()
    } catch {
      // start() lỗi (vd WS bị chặn bởi proxy/firewall) — poll vẫn chạy như trước, không throw
      // lên caller (AppLayout.vue không cần biết realtime có kết nối được hay không).
      isRealtimeConnected = false
    }
  }

  async function disconnectRealtime() {
    isRealtimeConnected = false
    await connection?.stop()
    connection = null
  }

  return {
    unreadCount,
    fetchUnreadCount,
    fetchList,
    markAsRead,
    markAllAsRead,
    startPolling,
    stopPolling,
    connectRealtime,
    disconnectRealtime,
  }
})
