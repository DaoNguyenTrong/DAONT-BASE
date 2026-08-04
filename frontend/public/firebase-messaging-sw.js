// Static asset served as-is from public/ — not processed by Vite. Firebase config is public/
// non-secret (same category as the OAuth client IDs used elsewhere in this app), so it's passed
// via the registration query string and read here from self.location.search — this repo has no
// PWA build plugin to template this file, and the config values can't change without a full
// browser reload anyway (they come from the same page that registers this worker).
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
