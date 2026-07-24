import type { Account, AuthResponse } from '@/api/types'

export function makeAccount(overrides: Partial<Account> = {}): Account {
  return {
    id: '00000000-0000-0000-0000-000000000001',
    name: 'Test User',
    username: 'test',
    email: 'test@example.com',
    emailConfirmed: true,
    phone: null,
    position: null,
    address: null,
    status: true,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: null,
    ...overrides,
  }
}

export function makeAuthResponse(overrides: Partial<Account> = {}): AuthResponse {
  return {
    accessToken: 'access-token',
    refreshToken: 'refresh-token',
    accessTokenExpiry: '2026-12-31T00:00:00Z',
    account: makeAccount(overrides),
  }
}
