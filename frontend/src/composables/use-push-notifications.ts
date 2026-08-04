import {
  deleteToken,
  getMessaging,
  getToken,
  isSupported as isMessagingSupported,
  onMessage,
  type Messaging,
} from 'firebase/messaging'
import { initializeApp, type FirebaseApp } from 'firebase/app'
import { getPushSubscriptions } from '@/api/generated/push-subscriptions/push-subscriptions'

const firebaseConfig = {
  apiKey: import.meta.env.VITE_FIREBASE_API_KEY ?? '',
  authDomain: import.meta.env.VITE_FIREBASE_AUTH_DOMAIN ?? '',
  projectId: import.meta.env.VITE_FIREBASE_PROJECT_ID ?? '',
  messagingSenderId: import.meta.env.VITE_FIREBASE_MESSAGING_SENDER_ID ?? '',
  appId: import.meta.env.VITE_FIREBASE_APP_ID ?? '',
}
const vapidKey = import.meta.env.VITE_FIREBASE_VAPID_KEY
const isConfigured = Boolean(vapidKey && firebaseConfig.apiKey)

// Module-level singletons — the SDK/service-worker registration only needs to happen once per
// page load, regardless of how many components use this composable.
let firebaseApp: FirebaseApp | null = null
let messaging: Messaging | null = null
let swRegistration: ServiceWorkerRegistration | null = null

async function ensureMessaging(): Promise<Messaging> {
  if (messaging) {
    return messaging
  }

  swRegistration = await navigator.serviceWorker.register(
    `/firebase-messaging-sw.js?${new URLSearchParams(firebaseConfig).toString()}`,
  )
  firebaseApp = initializeApp(firebaseConfig)
  messaging = getMessaging(firebaseApp)

  onMessage(messaging, (payload) => {
    const title = payload.notification?.title ?? payload.data?.title
    if (title) {
      showInfoMessage(title, payload.notification?.body ?? payload.data?.body)
    }
  })

  return messaging
}

export function usePushNotifications() {
  const isSupported = ref(false)
  const permission = ref<NotificationPermission>('default')
  const isSubscribed = ref(false)
  const currentToken = ref<string | null>(null)
  const pushClient = getPushSubscriptions()
  const { run, isLoading } = useApiAction()

  async function subscribe() {
    if (!isConfigured || !isSupported.value) {
      return
    }

    permission.value = await Notification.requestPermission()
    if (permission.value !== 'granted') {
      return
    }

    const messagingInstance = await ensureMessaging()
    const token = await getToken(messagingInstance, {
      vapidKey,
      serviceWorkerRegistration: swRegistration!,
    })
    if (!token) {
      return
    }

    await run(() => pushClient.pushSubscriptionsRegister({ token, platform: 'Web' }))
    currentToken.value = token
    isSubscribed.value = true
  }

  async function unsubscribe() {
    if (!isSubscribed.value) {
      return
    }

    const messagingInstance = await ensureMessaging()
    if (currentToken.value) {
      await run(() => pushClient.pushSubscriptionsRemove({ token: currentToken.value! }))
    }
    await deleteToken(messagingInstance)
    currentToken.value = null
    isSubscribed.value = false
  }

  onMounted(async () => {
    if (!isConfigured || typeof Notification === 'undefined') {
      return
    }

    isSupported.value = await isMessagingSupported()
    permission.value = Notification.permission
    if (!isSupported.value) {
      return
    }

    const status = await run(() => pushClient.pushSubscriptionsGetStatus())
    isSubscribed.value = status?.isActive ?? false

    if (isSubscribed.value && permission.value === 'granted') {
      const messagingInstance = await ensureMessaging()
      currentToken.value = await getToken(messagingInstance, {
        vapidKey,
        serviceWorkerRegistration: swRegistration!,
      }).catch(() => null)
    }
  })

  return { isSupported, permission, isSubscribed, isLoading, subscribe, unsubscribe }
}
