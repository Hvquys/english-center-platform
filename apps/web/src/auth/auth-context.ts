import { createContext, useContext } from 'react'
import type { ApiRequestOptions } from '../api/http'
import type { UserResponse } from '../api/contracts'

export interface AuthContextValue {
  user: UserResponse | null
  isAuthenticated: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
  request: <T>(path: string, options?: ApiRequestOptions) => Promise<T>
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used inside AuthProvider.')
  return context
}
