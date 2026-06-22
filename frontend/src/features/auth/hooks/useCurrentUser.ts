import { useQuery } from '@tanstack/react-query'
import { authService } from '@/features/auth/api/authService'

export function useCurrentUser(enabled: boolean) {
  return useQuery({
    queryKey: ['currentUser'],
    queryFn: authService.getCurrentUser,
    enabled,
    staleTime: 5 * 60 * 1000,
  })
}
