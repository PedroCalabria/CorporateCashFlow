import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { authService } from '@/features/auth/api/authService'
import {
  AUTH_EXPIRES_AT_KEY,
  AUTH_REFRESH_TOKEN_KEY,
  AUTH_TOKEN_KEY,
} from '@/features/auth/types/auth.types'
import { notifyAuthTokenChange } from '@/lib/auth-token-store'

export function useLogout() {
  const queryClient = useQueryClient()
  const navigate = useNavigate()

  return useMutation({
    mutationFn: authService.logout,
    onSuccess: () => {
      localStorage.removeItem(AUTH_TOKEN_KEY)
      localStorage.removeItem(AUTH_REFRESH_TOKEN_KEY)
      localStorage.removeItem(AUTH_EXPIRES_AT_KEY)
      notifyAuthTokenChange()
      queryClient.clear()
      navigate('/login')
    },
  })
}
