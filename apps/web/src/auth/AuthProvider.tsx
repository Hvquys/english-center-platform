import {
  type PropsWithChildren,
  useCallback,
  useMemo,
  useRef,
  useState,
} from 'react'
import type { AuthTokenResponse } from '../api/contracts'
import { ApiError, apiRequest, type ApiRequestOptions } from '../api/http'
import { AuthContext } from './auth-context'

const sessionKey = 'english-center.auth.session.v1'
type AuthSession = AuthTokenResponse

function isSession(value: unknown): value is AuthSession {
  if (!value || typeof value !== 'object') return false
  const session = value as Partial<AuthSession>
  return (
    typeof session.accessToken === 'string' &&
    typeof session.refreshToken === 'string' &&
    typeof session.accessTokenExpiresAtUtc === 'string' &&
    typeof session.refreshTokenExpiresAtUtc === 'string' &&
    typeof session.user?.email === 'string'
  )
}

function readSession() {
  try {
    const raw = window.sessionStorage.getItem(sessionKey)
    if (!raw) return null
    const session: unknown = JSON.parse(raw)
    if (!isSession(session) || Date.parse(session.refreshTokenExpiresAtUtc) <= Date.now()) {
      window.sessionStorage.removeItem(sessionKey)
      return null
    }
    return session
  } catch {
    window.sessionStorage.removeItem(sessionKey)
    return null
  }
}

function writeSession(session: AuthSession | null) {
  if (session) window.sessionStorage.setItem(sessionKey, JSON.stringify(session))
  else window.sessionStorage.removeItem(sessionKey)
}

export function AuthProvider({ children }: PropsWithChildren) {
  const [session, setSession] = useState<AuthSession | null>(readSession)
  const sessionRef = useRef(session)
  const refreshPromise = useRef<Promise<AuthSession> | null>(null)

  const updateSession = useCallback((nextSession: AuthSession | null) => {
    sessionRef.current = nextSession
    setSession(nextSession)
    writeSession(nextSession)
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    const nextSession = await apiRequest<AuthTokenResponse>('/auth/login', {
      method: 'POST',
      body: { email, password },
    })
    updateSession(nextSession)
  }, [updateSession])

  const refresh = useCallback(async () => {
    if (refreshPromise.current) return refreshPromise.current
    const current = sessionRef.current
    if (!current) throw new ApiError(401, { title: 'Phiên đăng nhập không tồn tại.' })

    const operation = apiRequest<AuthTokenResponse>('/auth/refresh', {
      method: 'POST',
      body: { refreshToken: current.refreshToken },
    })
      .then((nextSession) => {
        updateSession(nextSession)
        return nextSession
      })
      .catch((error: unknown) => {
        updateSession(null)
        throw error
      })
      .finally(() => {
        refreshPromise.current = null
      })

    refreshPromise.current = operation
    return operation
  }, [updateSession])

  const accessToken = useCallback(async () => {
    const current = sessionRef.current
    if (!current) throw new ApiError(401, { title: 'Bạn cần đăng nhập.' })
    const hasUsableAccessToken = Date.parse(current.accessTokenExpiresAtUtc) > Date.now() + 15_000
    return hasUsableAccessToken ? current.accessToken : (await refresh()).accessToken
  }, [refresh])

  const request = useCallback(async <T,>(path: string, options: ApiRequestOptions = {}) => {
    const token = await accessToken()
    try {
      return await apiRequest<T>(path, { ...options, accessToken: token })
    } catch (error) {
      if (!(error instanceof ApiError) || error.status !== 401) throw error
      const nextSession = await refresh()
      return apiRequest<T>(path, { ...options, accessToken: nextSession.accessToken })
    }
  }, [accessToken, refresh])

  const logout = useCallback(async () => {
    const current = sessionRef.current
    updateSession(null)
    if (!current) return
    try {
      await apiRequest<void>('/auth/logout', {
        method: 'POST',
        accessToken: current.accessToken,
        body: { refreshToken: current.refreshToken },
      })
    } catch {
      // The local session is already cleared. An expired server session needs no retry.
    }
  }, [updateSession])

  const value = useMemo(() => ({
    user: session?.user ?? null,
    isAuthenticated: Boolean(session),
    login,
    logout,
    request,
  }), [login, logout, request, session])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
