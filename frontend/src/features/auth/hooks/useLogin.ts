import { useMutation, useQueryClient } from '@tanstack/react-query'
import { authService } from '@/features/auth/api/authService'
import {
  AUTH_EXPIRES_AT_KEY,
  AUTH_REFRESH_TOKEN_KEY,
  AUTH_TOKEN_KEY,
} from '@/features/auth/types/auth.types'
import { notifyAuthTokenChange } from '@/lib/auth-token-store'

export function useLogin() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: authService.login,
    onSuccess: (data) => {
      localStorage.setItem(AUTH_TOKEN_KEY, data.token)
      localStorage.setItem(AUTH_REFRESH_TOKEN_KEY, data.refreshToken)
      localStorage.setItem(AUTH_EXPIRES_AT_KEY, data.expiresAt)
      notifyAuthTokenChange()
      void queryClient.invalidateQueries({ queryKey: ['currentUser'] })
    },
  })
}
