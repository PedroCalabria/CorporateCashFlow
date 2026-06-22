import { createContext, useContext, type ReactNode } from 'react'
import { useCurrentUser } from '@/features/auth/hooks/useCurrentUser'
import { useHasAuthToken } from '@/lib/auth-token-store'
import type { UserContextResponse } from '@/features/auth/types/auth.types'

interface AuthContextValue {
  user: UserContextResponse | null
  isAuthenticated: boolean
  isLoading: boolean
  isHydratingSession: boolean
  hasToken: boolean
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthContextProvider({ children }: { children: ReactNode }) {
  const hasToken = useHasAuthToken()
  const { data: user, isLoading: isQueryLoading, isError, isFetching } = useCurrentUser(hasToken)

  const isHydratingSession = hasToken && !user && !isError
  const isAuthenticated = !!user && !isError
  const isLoading = isHydratingSession || (hasToken && (isQueryLoading || isFetching) && !user)

  const value: AuthContextValue = {
    user: user ?? null,
    isAuthenticated,
    isLoading,
    isHydratingSession,
    hasToken,
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuthContext() {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuthContext must be used within AuthContextProvider')
  }
  return context
}
