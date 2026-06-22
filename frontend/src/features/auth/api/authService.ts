import { axiosInstance } from '@/lib/axios'
import type {
  LoginRequest,
  LoginResponse,
  TokenRefreshRequest,
  TokenRefreshResponse,
  UserContextResponse,
} from '@/features/auth/types/auth.types'

export const authService = {
  login(req: LoginRequest): Promise<LoginResponse> {
    return axiosInstance.post<LoginResponse>('/auth/login', req).then((r) => r.data)
  },

  getCurrentUser(): Promise<UserContextResponse> {
    return axiosInstance.get<UserContextResponse>('/auth/me').then((r) => r.data)
  },

  refreshToken(req: TokenRefreshRequest): Promise<TokenRefreshResponse> {
    return axiosInstance.post<TokenRefreshResponse>('/auth/refresh', req).then((r) => r.data)
  },

  logout(): Promise<void> {
    return axiosInstance.post('/auth/logout').then(() => undefined)
  },
}
