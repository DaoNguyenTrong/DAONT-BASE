import { getAuth } from '@/api/generated/auth/auth'
import { refreshClient } from '@/api/client'
import { reloadToLogin } from '@/lib/session-exit'
import type {
  Account,
  AuthResponse,
  ExternalLoginRequest,
  LoginRequest,
  LogoutRequest,
  RefreshTokenRequest,
  RegisterRequest,
  RegisterResult,
  ResendVerificationRequest,
  SessionDto,
  VerifyEmailRequest,
} from '@/api/types'

export const useAuthStore = defineStore('auth', () => {
  const account = ref<Account | null>(null)
  const organizationId = ref<string | null>(null)
  const organizationName = ref<string | null>(null)
  // Active-org permissions, refreshed on login/refreshToken; an org switch reloads the page
  // (useOrganizationSwitch). Same staleness as organizationId/organizationName. UI-only gate:
  // the server's per-request permission check is the real authorization boundary.
  const permissions = ref<string[]>([])
  const client = getAuth()

  const isAuthenticated = computed(() => account.value !== null)

  function setAuth(response: AuthResponse) {
    account.value = response.account
    organizationId.value = response.organizationId
    organizationName.value = response.organizationName
    permissions.value = response.permissions
    // Breadcrumb for the next boot: a silent refresh is worth attempting.
    markSessionHint()
  }

  function clearAuth() {
    account.value = null
    organizationId.value = null
    organizationName.value = null
    permissions.value = []
    clearSessionHint()
  }

  function hasPermission(code: string): boolean {
    return permissions.value.includes(code)
  }

  async function login(data: LoginRequest): Promise<AuthResponse> {
    const response = await client.authLogin(data)
    setAuth(response)
    return response
  }

  async function register(data: RegisterRequest): Promise<RegisterResult> {
    return client.authRegister(data)
  }

  async function verifyEmail(data: VerifyEmailRequest): Promise<AuthResponse> {
    const response = await client.authVerifyEmail(data)
    setAuth(response)
    return response
  }

  async function resendVerification(data: ResendVerificationRequest): Promise<void> {
    await client.authResendVerification(data)
  }

  async function externalLogin(
    provider: string,
    data: ExternalLoginRequest,
  ): Promise<AuthResponse> {
    const response = await client.authExternalLogin(provider, data)
    setAuth(response)
    return response
  }

  // Hand-written, not routed through the generated client: must go through `refreshClient` — a
  // separate axios instance with no 401-retry interceptor — so a failed silent refresh on app
  // boot can't recurse into apiClient's own refresh-and-retry logic.
  async function refreshToken(data?: RefreshTokenRequest): Promise<AuthResponse> {
    const response = await refreshClient.post<AuthResponse>('/api/auth/refresh', data ?? {})
    setAuth(response.data)
    return response.data
  }

  async function logout(data?: LogoutRequest): Promise<void> {
    try {
      await client.authLogout(data)
    } finally {
      clearKeepLoginPreference()
      clearAuth()
      // Synchronously after clearAuth: App.vue's session-lapse watcher reloads to
      // login?redirect=<current page> on the same flip. This plain reload must win (first call
      // wins), or the next account to sign in here lands on the previous account's page.
      reloadToLogin()
    }
  }

  async function getSessions(): Promise<SessionDto[]> {
    const sessions = await client.authGetSessions()
    return sessions.map((session) => ({ ...session, id: Number(session.id) }))
  }

  async function revokeSession(id: number): Promise<void> {
    await client.authRevokeSession(id)
  }

  async function revokeOtherSessions(): Promise<void> {
    await client.authRevokeOtherSessions()
  }

  return {
    account,
    organizationId,
    organizationName,
    permissions,
    isAuthenticated,
    setAuth,
    clearAuth,
    hasPermission,
    login,
    register,
    verifyEmail,
    resendVerification,
    externalLogin,
    refreshToken,
    logout,
    getSessions,
    revokeSession,
    revokeOtherSessions,
  }
})
