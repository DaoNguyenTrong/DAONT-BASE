import { apiClient, refreshClient } from './client'
import type {
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
} from './types'

const login = async (data: LoginRequest): Promise<AuthResponse> => {
  const response = await apiClient.post<AuthResponse>('/api/auth/login', data)
  useAuthStore().setAuth(response.data)
  return response.data
}

const register = async (data: RegisterRequest): Promise<RegisterResult> => {
  const response = await apiClient.post<RegisterResult>('/api/auth/register', data)
  return response.data
}

const verifyEmail = async (data: VerifyEmailRequest): Promise<AuthResponse> => {
  const response = await apiClient.post<AuthResponse>('/api/auth/verify-email', data)
  useAuthStore().setAuth(response.data)
  return response.data
}

const resendVerification = async (data: ResendVerificationRequest): Promise<void> => {
  await apiClient.post('/api/auth/resend-verification', data)
}

const externalLogin = async (
  provider: string,
  data: ExternalLoginRequest,
): Promise<AuthResponse> => {
  const response = await apiClient.post<AuthResponse>(`/api/auth/external/${provider}`, data)
  useAuthStore().setAuth(response.data)
  return response.data
}

const refreshToken = async (data?: RefreshTokenRequest): Promise<AuthResponse> => {
  const response = await refreshClient.post<AuthResponse>('/api/auth/refresh', data ?? {})
  useAuthStore().setAuth(response.data)
  return response.data
}

const logout = async (data?: LogoutRequest): Promise<void> => {
  try {
    await apiClient.post('/api/auth/logout', data ?? {})
  } finally {
    clearKeepLoginPreference()
    useAuthStore().clearAuth()
  }
}

const getSessions = async (): Promise<SessionDto[]> => {
  const response = await apiClient.get<SessionDto[]>('/api/auth/sessions')
  return response.data
}

const revokeSession = async (id: number): Promise<void> => {
  await apiClient.delete(`/api/auth/sessions/${id}`)
}

const revokeOtherSessions = async (): Promise<void> => {
  await apiClient.post('/api/auth/sessions/revoke-others')
}

export default {
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
