import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuthContext } from '@/features/auth/components/AuthContext'
import type { UserRole } from '@/features/auth/types/auth.types'

function LoadingSpinner() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-background">
      <p className="text-sm text-muted-foreground">Loading...</p>
    </div>
  )
}

interface ProtectedRouteProps {
  allowedRoles?: UserRole[]
}

export function ProtectedRoute({ allowedRoles }: ProtectedRouteProps) {
  const { isAuthenticated, isLoading, isHydratingSession, user } = useAuthContext()
  const location = useLocation()

  if (isLoading || isHydratingSession) {
    return <LoadingSpinner />
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ redirect: location.pathname }} replace />
  }

  if (allowedRoles && user && !allowedRoles.includes(user.role)) {
    return <Navigate to="/unauthorized" replace />
  }

  return <Outlet />
}
