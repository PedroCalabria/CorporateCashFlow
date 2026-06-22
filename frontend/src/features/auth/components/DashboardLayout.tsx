import { useLogout } from '@/features/auth/hooks/useLogout'
import { useAuthContext } from '@/features/auth/components/AuthContext'
import { Button } from '@/components/ui/button'
import { ThemeToggle } from '@/components/theme/ThemeToggle'

export function DashboardLayout() {
  const { user } = useAuthContext()
  const logout = useLogout()

  return (
    <div className="min-h-screen bg-background p-6 text-foreground">
      <header className="mb-6 flex items-center justify-between border-b border-border pb-4">
        <div>
          <h1 className="text-xl font-semibold">Dashboard</h1>
          {user && (
            <p className="text-sm text-muted-foreground">
              {user.name} · {user.role}
            </p>
          )}
        </div>
        <div className="flex items-center gap-2">
          <ThemeToggle />
          <Button variant="outline" onClick={() => logout.mutate()} disabled={logout.isPending}>
            Logout
          </Button>
        </div>
      </header>
      <p>Welcome to Corporate Cash Flow.</p>
    </div>
  )
}
