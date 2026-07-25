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

// Pagination fields come back from the generated PagedResultOf*Dto types as `number | string`
// (the .NET OpenAPI generator widens int64/int32 response fields to a string-pattern union for
// JS-safe-integer interop). The api layer normalizes to this stable, number-only shape before
// returning — keeps call sites doing plain arithmetic/comparisons on pageNumber/totalPages etc.
export interface PagedResult<T> {
  items: T[]
  totalCount: number
  pageNumber: number
  pageSize: number
  totalPages: number
  hasPreviousPage: boolean
  hasNextPage: boolean
}

export type { HealthResponse } from './generated/model/healthResponse'
export type { AccountDto as Account } from './generated/model/accountDto'
export type { LoginResponse as AuthResponse } from './generated/model/loginResponse'
export type { LoginRequest } from './generated/model/loginRequest'
export type { RegisterRequest } from './generated/model/registerRequest'
export type { RegisterResult } from './generated/model/registerResult'
export type { VerifyEmailRequest } from './generated/model/verifyEmailRequest'
export type { ResendVerificationRequest } from './generated/model/resendVerificationRequest'
export type { ExternalLoginRequest } from './generated/model/externalLoginRequest'
export type { RefreshTokenRequest } from './generated/model/refreshTokenRequest'
export type { RefreshTokenRequest as LogoutRequest } from './generated/model/refreshTokenRequest'
export type { CreateAccountRequest } from './generated/model/createAccountRequest'
export type { UpdateAccountRequest } from './generated/model/updateAccountRequest'
export type { ProfileDto } from './generated/model/profileDto'
export type { UpdateProfileRequest as ProfileUpdateRequest } from './generated/model/updateProfileRequest'
export type { ChangePasswordRequest } from './generated/model/changePasswordRequest'

import type { AccountDto as Account } from './generated/model/accountDto'

export type AccountPagedResult = PagedResult<Account>

// Hand-kept: generated SessionDto.id is `number | string` (same int64-as-string widening as
// pagination, see PagedResult above) — normalized to plain `number` by auth-api.ts.
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
