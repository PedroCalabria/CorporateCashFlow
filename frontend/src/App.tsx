import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { ThemeProvider } from '@/components/theme/ThemeProvider'
import { AuthContextProvider } from '@/features/auth/components/AuthContext'
import { DashboardLayout } from '@/features/auth/components/DashboardLayout'
import { LoginForm } from '@/features/auth/components/LoginForm'
import { ProtectedRoute } from '@/features/auth/components/ProtectedRoute'

const queryClient = new QueryClient()

function App() {
  return (
    <ThemeProvider>
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>
          <AuthContextProvider>
            <Routes>
              <Route path="/login" element={<LoginForm />} />
              <Route element={<ProtectedRoute />}>
                <Route path="/dashboard" element={<DashboardLayout />} />
              </Route>
              <Route element={<ProtectedRoute allowedRoles={['Admin']} />}>
                <Route path="/admin" element={<div className="bg-background p-6 text-foreground">Admin area</div>} />
              </Route>
              <Route path="/unauthorized" element={<div className="bg-background p-6 text-foreground">Unauthorized</div>} />
              <Route path="*" element={<Navigate to="/login" replace />} />
            </Routes>
          </AuthContextProvider>
        </BrowserRouter>
        {import.meta.env.DEV && <ReactQueryDevtools initialIsOpen={false} />}
      </QueryClientProvider>
    </ThemeProvider>
  )
}

export default App
