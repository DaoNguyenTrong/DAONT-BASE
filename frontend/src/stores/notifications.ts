import * as signalR from '@microsoft/signalr'
import { getNotifications } from '@/api/generated/notifications/notifications'
import { toPagedResult } from '@/lib/paged-result'
import type { Notification } from '@/api/types'
import { apiClient } from '@/api/client'

const POLL_INTERVAL_MS = 30_000

export const useNotificationsStore = defineStore('notifications', () => {
  const unreadCount = ref(0)
  const client = getNotifications()
  let intervalHandle: ReturnType<typeof setInterval> | null = null
  let connection: signalR.HubConnection | null = null
  let isRealtimeConnected = false
  // true chỉ trong khoảng startPolling → stopPolling (app shell đang mount). connection.stop() lúc
  // teardown vẫn bắn onclose → resume(); thiếu cờ này poll sẽ tự sống lại ở trang login, gọi API
  // mỗi 30s và nhận 401. Chỉ startPolling bật cờ: realtime không tự kích hoạt poll fallback.
  let isActive = false

  async function fetchUnreadCount() {
    const response = await client.notificationsGetUnreadCount()
    unreadCount.value = Number(response.count)
  }

  async function fetchList(pageNumber: number, pageSize: number) {
    const response = await client.notificationsGetAll({ pageNumber, pageSize })
    return toPagedResult<Notification>(response)
  }

  // Cho các lần gọi không ai await (interval, visibilitychange, SignalR callback): lỗi mạng hay 401
  // ở đây chỉ có nghĩa là lần poll sau thử lại, không được thành unhandled rejection.
  function fetchUnreadCountInBackground() {
    fetchUnreadCount().catch(() => {})
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
    if (!isActive) return
    fetchUnreadCountInBackground()
    // Không schedule interval khi SignalR đang sống — poll chỉ là fallback, tránh double-fetch
    // dư thừa trong lúc kênh realtime đã đang giao hàng (xem connectRealtime()).
    if (!isRealtimeConnected) {
      intervalHandle ??= setInterval(fetchUnreadCountInBackground, POLL_INTERVAL_MS)
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
    isActive = true
    document.addEventListener('visibilitychange', handleVisibilityChange)
    if (document.visibilityState === 'visible') resume()
  }

  function stopPolling() {
    isActive = false
    pause()
    document.removeEventListener('visibilitychange', handleVisibilityChange)
  }

  async function connectRealtime() {
    // Lấy từ apiClient (main.ts đã áp runtime config.json) thay vì import.meta.env, để một bundle
    // deploy được ở nhiều môi trường. Bỏ '/' cuối: 'https://x/' + '/hubs' sẽ thành '//hubs'.
    const apiBaseUrl = (apiClient.defaults.baseURL ?? '').replace(/\/+$/, '')
    const current = new signalR.HubConnectionBuilder()
      .withUrl(`${apiBaseUrl}/hubs/notifications`, { withCredentials: true })
      .withAutomaticReconnect()
      .build()
    connection = current
    // Logout → re-login có thể dựng connection mới trước khi stop() của connection cũ xong; khi đó
    // onclose của connection cũ không được đụng vào state của phiên mới.
    const isCurrent = () => connection === current

    current.on('ReceiveNotification', (_dto: Notification) => {
      if (!isCurrent()) return
      unreadCount.value += 1
    })

    current.onreconnecting(() => {
      if (!isCurrent()) return
      // Đang trong cửa sổ retry của withAutomaticReconnect (backoff mặc định tới ~30-60s) —
      // CHƯA phải onclose, nhưng kết nối đã mất. Phải resume poll ngay ở đây, không đợi
      // onclose, nếu không sẽ có khoảng trống không kênh nào đang giao hàng.
      isRealtimeConnected = false
      resume()
    })

    current.onreconnected(() => {
      if (!isCurrent()) return
      isRealtimeConnected = true
      pause() // dừng poll trở lại khi WS sống lại
      fetchUnreadCountInBackground() // catch-up: bù các notification có thể miss lúc mất kết nối
    })

    current.onclose(() => {
      if (!isCurrent()) return
      isRealtimeConnected = false
      resume() // fallback: SignalR hết lượt tự reconnect (hoặc chưa từng start được), quay lại poll
    })

    try {
      await current.start()
      if (!isCurrent()) return
      isRealtimeConnected = true
      pause()
    } catch {
      // start() lỗi (vd WS bị chặn bởi proxy/firewall) — poll vẫn chạy như trước, không throw
      // lên caller (AppLayout.vue không cần biết realtime có kết nối được hay không).
      if (isCurrent()) isRealtimeConnected = false
    }
  }

  async function disconnectRealtime() {
    // Tách connection ra trước khi await: nếu app shell mount lại trong lúc chờ stop(), connection
    // mới không được bị gán null (rò WS, lần teardown sau không stop được nó).
    const closing = connection
    connection = null
    isRealtimeConnected = false
    await closing?.stop()
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
