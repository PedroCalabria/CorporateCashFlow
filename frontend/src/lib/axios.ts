import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios'
import {
  AUTH_EXPIRES_AT_KEY,
  AUTH_REFRESH_TOKEN_KEY,
  AUTH_TOKEN_KEY,
  type TokenRefreshResponse,
} from '@/features/auth/types/auth.types'
import { notifyAuthTokenChange } from '@/lib/auth-token-store'

interface RetryConfig extends InternalAxiosRequestConfig {
  _retry?: boolean
}

function shouldSkipTokenRefresh(url?: string): boolean {
  return !!url?.includes('/auth/login') || !!url?.includes('/auth/refresh')
}

export const axiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? '/api',
})

axiosInstance.interceptors.request.use((config) => {
  const token = localStorage.getItem(AUTH_TOKEN_KEY)
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

axiosInstance.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as RetryConfig | undefined

    if (
      !originalRequest ||
      originalRequest._retry ||
      error.response?.status !== 401 ||
      shouldSkipTokenRefresh(originalRequest.url)
    ) {
      return Promise.reject(error)
    }

    originalRequest._retry = true

    const accessToken = localStorage.getItem(AUTH_TOKEN_KEY)
    const refreshToken = localStorage.getItem(AUTH_REFRESH_TOKEN_KEY)

    if (!accessToken || !refreshToken) {
      clearAuthStorage()
      window.location.replace('/login')
      return Promise.reject(error)
    }

    try {
      const { data } = await axios.post<TokenRefreshResponse>(
        `${import.meta.env.VITE_API_BASE_URL ?? '/api'}/auth/refresh`,
        { accessToken, refreshToken },
      )

      localStorage.setItem(AUTH_TOKEN_KEY, data.accessToken)
      localStorage.setItem(AUTH_REFRESH_TOKEN_KEY, data.refreshToken)
      localStorage.setItem(AUTH_EXPIRES_AT_KEY, data.expiresAt)
      notifyAuthTokenChange()

      originalRequest.headers.Authorization = `Bearer ${data.accessToken}`
      return axiosInstance(originalRequest)
    } catch {
      clearAuthStorage()
      window.location.replace('/login')
      return Promise.reject(error)
    }
  },
)

function clearAuthStorage() {
  localStorage.removeItem(AUTH_TOKEN_KEY)
  localStorage.removeItem(AUTH_REFRESH_TOKEN_KEY)
  localStorage.removeItem(AUTH_EXPIRES_AT_KEY)
  notifyAuthTokenChange()
}
