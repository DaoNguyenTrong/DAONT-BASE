// Served as-is from public/ (not processed by Vite; no PWA plugin to template it), so the
// public, non-secret Firebase config arrives via the registration query string.
importScripts('https://www.gstatic.com/firebasejs/12.17.0/firebase-app-compat.js')
importScripts('https://www.gstatic.com/firebasejs/12.17.0/firebase-messaging-compat.js')

const params = new URLSearchParams(self.location.search)

firebase.initializeApp({
  apiKey: params.get('apiKey'),
  authDomain: params.get('authDomain'),
  projectId: params.get('projectId'),
  messagingSenderId: params.get('messagingSenderId'),
  appId: params.get('appId'),
})

const messaging = firebase.messaging()

messaging.onBackgroundMessage((payload) => {
  const title = payload.notification?.title ?? payload.data?.title
  const body = payload.notification?.body ?? payload.data?.body

  if (!title) {
    return
  }

  self.registration.showNotification(title, { body })
})
