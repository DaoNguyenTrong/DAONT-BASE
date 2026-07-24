export interface ProblemDetails {
  status: number
  title: string
  detail?: string
  type?: string
  code?: string
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails

  constructor(problem: ProblemDetails) {
    super(problem.detail ?? problem.title)
    this.name = 'ApiError'
    this.status = problem.status
    this.problem = problem
  }
}

export interface HealthResponse {
  status: string
  version: string
  timestamp: string
  buildTimestamp: string | null
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  pageNumber: number
  pageSize: number
  totalPages: number
  hasPreviousPage: boolean
  hasNextPage: boolean
}

export interface Account {
  id: string
  name: string
  username: string
  email: string
  emailConfirmed: boolean
  phone: string | null
  position: string | null
  address: string | null
  status: boolean
  createdAt: string
  updatedAt: string | null
}

export type AccountPagedResult = PagedResult<Account>

export interface AuthResponse {
  accessToken: string
  refreshToken: string
  accessTokenExpiry: string
  account: Account
}

export interface LoginRequest {
  username: string
  password: string
  keepLoggedIn?: boolean
}

export interface RegisterRequest {
  name: string
  username: string
  email: string
  password: string
  phone?: string
  position?: string
  address?: string
}

export interface RegisterResult {
  accountId: string
  email: string
}

export interface VerifyEmailRequest {
  token: string
}

export interface ResendVerificationRequest {
  email: string
}

export interface ExternalLoginRequest {
  credential: string
}

export interface RefreshTokenRequest {
  refreshToken?: string
}

export interface LogoutRequest {
  refreshToken?: string
}

export interface CreateAccountRequest {
  name: string
  phone?: string | null
  position?: string | null
  address?: string | null
  username: string
  email: string
  password: string
  status: boolean
}

export interface UpdateAccountRequest {
  name: string
  phone?: string | null
  position?: string | null
  address?: string | null
  username: string
  email: string
  status: boolean
}

export interface ProfileDto {
  id: string
  name: string
  username: string
  email: string
  emailConfirmed: boolean
  phone: string | null
  position: string | null
  address: string | null
  hasPassword: boolean
}

export interface ProfileUpdateRequest {
  name: string
  phone?: string | null
  position?: string | null
  address?: string | null
  email: string
}

export interface ChangePasswordRequest {
  currentPassword: string
  newPassword: string
}

export interface SessionDto {
  id: number
  deviceInfo: string | null
  ipAddress: string | null
  isPersistent: boolean
  isCurrent: boolean
  loginAt: string
  lastActiveAt: string
  expiresAt: string
}
