/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL: string
  readonly VITE_GOOGLE_CLIENT_ID?: string
  readonly VITE_MICROSOFT_CLIENT_ID?: string
  readonly VITE_MICROSOFT_TENANT_ID?: string
  readonly VITE_FIREBASE_API_KEY?: string
  readonly VITE_FIREBASE_AUTH_DOMAIN?: string
  readonly VITE_FIREBASE_PROJECT_ID?: string
  readonly VITE_FIREBASE_MESSAGING_SENDER_ID?: string
  readonly VITE_FIREBASE_APP_ID?: string
  readonly VITE_FIREBASE_VAPID_KEY?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}

interface CredentialResponse {
  credential: string
}

interface Window {
  google?: {
    accounts: {
      id: {
        initialize(config: {
          client_id: string
          callback: (response: CredentialResponse) => void
        }): void
        prompt(): void
      }
    }
  }
}

declare module 'virtual:svg-icons-register' {
  const component: any
  export default component
}
