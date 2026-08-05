import axios from 'axios'
import { AxiosHeaders } from 'axios'
import type { AxiosRequestHeaders } from 'axios'

import type { ProblemDetails } from './types'

declare module 'axios' {
  interface InternalAxiosRequestConfig {
    _retry?: boolean
  }
}

type FailedQueueItem = {
  resolve: () => void
  reject: (reason?: unknown) => void
}

let isRefreshing = false
let failedQueue: FailedQueueItem[] = []

function toProblemDetails(
  data: unknown,
  fallbackStatus: number,
  fallbackTitle: string,
): ProblemDetails | null {
  if (!data || typeof data !== 'object') {
    return null
  }

  const { detail, errors, status, title, type, code } = data as Partial<ProblemDetails>

  if (typeof status !== 'number') {
    return null
  }

  return {
    status,
    title: typeof title === 'string' && title.length > 0 ? title : fallbackTitle,
    detail: typeof detail === 'string' && detail.length > 0 ? detail : undefined,
    type: typeof type === 'string' ? type : undefined,
    code: typeof code === 'string' ? code : undefined,
    errors: errors && typeof errors === 'object' ? errors : undefined,
  }
}

function processFailedQueue(error: unknown) {
  failedQueue.forEach(({ reject, resolve }) => {
    if (error) {
      reject(error)
      return
    }

    resolve()
  })

  failedQueue = []
}

function toApiError(error: unknown): ApiError | null {
  if (!axios.isAxiosError(error) || !error.response) {
    return null
  }

  const contentType = error.response.headers['content-type']
  const hasProblemContentType =
    typeof contentType === 'string' && contentType.includes('application/problem+json')
  const problem = toProblemDetails(
    error.response.data,
    error.response.status,
    error.response.statusText || i18n.global.t('errors.requestFailed'),
  )

  if (!hasProblemContentType && !problem) {
    return null
  }

  return new ApiError(
    problem ?? {
      status: error.response.status,
      title: error.response.statusText || i18n.global.t('errors.requestFailed'),
    },
  )
}

function applyCommonHeaders(config: { headers?: AxiosRequestHeaders; url?: string }) {
  const headers = AxiosHeaders.from(config.headers)
  const localeSource = i18n.global.locale as string | { value: string }
  const locale = typeof localeSource === 'string' ? localeSource : localeSource.value

  if (config.url !== '/api/health') {
    const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone
    headers.set('X-TimeZone', timeZone === 'Asia/Saigon' ? 'Asia/Ho_Chi_Minh' : timeZone)
  }

  headers.set('Accept-Language', locale)

  // A cross-site <form> submission can't set a custom header, so a forged request never carries
  // this — the value is unchecked by the backend, only presence matters. See CsrfProtectionMiddleware.
  headers.set('X-CSRF-Protection', '1')

  return headers
}

function isAuthFlowRequest(url?: string) {
  return url?.startsWith('/api/auth/') ?? false
}

function isPasswordChangeRequest(url?: string) {
  return url === '/api/profile/password'
}

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  withCredentials: true,
  timeout: 300_000,
  headers: {
    'Content-Type': 'application/json',
  },
})

export const refreshClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  withCredentials: true,
  timeout: 300_000,
  headers: {
    'Content-Type': 'application/json',
  },
})

apiClient.interceptors.request.use((config) => {
  const headers = applyCommonHeaders(config)

  config.headers = headers
  return config
})

refreshClient.interceptors.request.use((config) => {
  config.headers = applyCommonHeaders(config)
  return config
})

apiClient.interceptors.response.use(
  (response) => response,
  (error: unknown) => {
    if (!axios.isAxiosError(error) || !error.response) {
      return Promise.reject(error)
    }

    const auth = useAuthStore()
    const originalRequest = error.config

    if (
      error.response.status === 401 &&
      originalRequest &&
      !isAuthFlowRequest(originalRequest.url) &&
      !isPasswordChangeRequest(originalRequest.url)
    ) {
      if (originalRequest._retry) {
        auth.clearAuth()
        return Promise.reject(toApiError(error) ?? error)
      }

      if (isRefreshing) {
        return new Promise((resolve, reject) => {
          failedQueue.push({
            resolve: () => resolve(apiClient(originalRequest)),
            reject,
          })
        })
      }

      originalRequest._retry = true
      isRefreshing = true

      return auth
        .refreshToken()
        .then(() => {
          processFailedQueue(null)
          return apiClient(originalRequest)
        })
        .catch((refreshError) => {
          const apiError = toApiError(refreshError) ?? refreshError
          clearKeepLoginPreference()
          auth.clearAuth()
          processFailedQueue(apiError)
          return Promise.reject(apiError)
        })
        .finally(() => {
          isRefreshing = false
        })
    }

    const apiError = toApiError(error)

    if (apiError) {
      return Promise.reject(apiError)
    }

    return Promise.reject(error)
  },
)
