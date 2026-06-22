export type UserRole = 'Admin' | 'Editor' | 'Auditor'

export interface LoginRequest {
  email: string
  password: string
}

export interface LoginResponse {
  token: string
  refreshToken: string
  expiresAt: string
}

export interface TokenRefreshRequest {
  accessToken: string
  refreshToken: string
}

export interface TokenRefreshResponse {
  accessToken: string
  refreshToken: string
  expiresAt: string
}

export interface UserContextResponse {
  id: string
  name: string
  email: string
  role: UserRole
  subsidiaryId: string | null
}

export interface ErrorResponse {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  errors?: Record<string, string[]>
}

export const AUTH_TOKEN_KEY = 'token'
export const AUTH_REFRESH_TOKEN_KEY = 'refreshToken'
export const AUTH_EXPIRES_AT_KEY = 'expiresAt'
