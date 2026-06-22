import { useSyncExternalStore } from 'react'
import { AUTH_TOKEN_KEY } from '@/features/auth/types/auth.types'

const listeners = new Set<() => void>()

function subscribe(listener: () => void) {
  listeners.add(listener)
  window.addEventListener('storage', listener)
  return () => {
    listeners.delete(listener)
    window.removeEventListener('storage', listener)
  }
}

function getSnapshot() {
  return !!localStorage.getItem(AUTH_TOKEN_KEY)
}

function getServerSnapshot() {
  return false
}

export function useHasAuthToken() {
  return useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot)
}

export function notifyAuthTokenChange() {
  for (const listener of listeners) {
    listener()
  }
}
