import { useMutation } from '@tanstack/react-query'
import { authService } from '@/features/auth/api/authService'
import {
  AUTH_EXPIRES_AT_KEY,
  AUTH_REFRESH_TOKEN_KEY,
  AUTH_TOKEN_KEY,
} from '@/features/auth/types/auth.types'

export function useRefreshToken() {
  return useMutation({
    mutationFn: authService.refreshToken,
    onSuccess: (data) => {
      localStorage.setItem(AUTH_TOKEN_KEY, data.accessToken)
      localStorage.setItem(AUTH_REFRESH_TOKEN_KEY, data.refreshToken)
      localStorage.setItem(AUTH_EXPIRES_AT_KEY, data.expiresAt)
    },
  })
}
