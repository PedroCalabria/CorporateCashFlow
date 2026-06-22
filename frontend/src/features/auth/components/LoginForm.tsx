import { zodResolver } from '@hookform/resolvers/zod'
import { useQueryClient } from '@tanstack/react-query'
import { isAxiosError } from 'axios'
import { useForm } from 'react-hook-form'
import { useLocation, useNavigate } from 'react-router-dom'
import { z } from 'zod'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { authService } from '@/features/auth/api/authService'
import { useLogin } from '@/features/auth/hooks/useLogin'
import type { ErrorResponse } from '@/features/auth/types/auth.types'

const loginSchema = z.object({
  email: z.string().email('Enter a valid email address'),
  password: z.string().min(8, 'Password must be at least 8 characters'),
})

type LoginFormValues = z.infer<typeof loginSchema>

function getLoginErrorMessage(error: unknown): string | undefined {
  if (!isAxiosError<ErrorResponse>(error)) {
    return error ? 'Unable to sign in. Please try again.' : undefined
  }

  const detail = error.response?.data?.detail
  if (detail) return detail

  const status = error.response?.status
  if (status === 401) return 'Invalid email or password.'
  if (status === 503) return 'Service is temporarily unavailable. Please try again later.'
  if (error.response) return 'Unable to sign in. Please try again.'

  return undefined
}

export function LoginForm() {
  const navigate = useNavigate()
  const location = useLocation()
  const queryClient = useQueryClient()
  const login = useLogin()

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  })

  const formError = getLoginErrorMessage(login.error)

  const onSubmit = handleSubmit(async (values) => {
    login.reset()
    try {
      await login.mutateAsync(values)
      await queryClient.fetchQuery({
        queryKey: ['currentUser'],
        queryFn: authService.getCurrentUser,
      })
      const redirect = (location.state as { redirect?: string } | null)?.redirect ?? '/dashboard'
      navigate(redirect, { replace: true })
    } catch (error) {
      if (isAxiosError<ErrorResponse>(error) && error.response?.status === 400) {
        const fieldErrors = error.response.data.errors ?? {}
        for (const [field, messages] of Object.entries(fieldErrors)) {
          if (field === 'email' || field === 'password') {
            setError(field, { message: messages[0] })
          }
        }
      }
    }
  })

  return (
    <div className="flex min-h-screen items-center justify-center bg-background p-4">
      <form
        onSubmit={onSubmit}
        className="w-full max-w-md space-y-4 rounded-lg border border-border bg-card p-6 shadow-sm"
      >
        <div className="space-y-1">
          <h1 className="text-2xl font-semibold text-card-foreground">Sign in</h1>
          <p className="text-sm text-muted-foreground">Corporate Cash Flow</p>
        </div>

        {formError && (
          <Alert variant="destructive">
            <AlertDescription>{formError}</AlertDescription>
          </Alert>
        )}

        <div className="space-y-2">
          <Label htmlFor="email">Email</Label>
          <Input id="email" type="email" autoComplete="email" {...register('email')} />
          {errors.email && <p className="text-sm text-destructive">{errors.email.message}</p>}
        </div>

        <div className="space-y-2">
          <Label htmlFor="password">Password</Label>
          <Input id="password" type="password" autoComplete="current-password" {...register('password')} />
          {errors.password && <p className="text-sm text-destructive">{errors.password.message}</p>}
        </div>

        <Button type="submit" className="w-full" disabled={login.isPending}>
          {login.isPending ? 'Signing in...' : 'Sign in'}
        </Button>
      </form>
    </div>
  )
}
