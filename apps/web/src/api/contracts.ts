export type AppRole = 'ADMIN' | 'STAFF' | 'TEACHER' | 'STUDENT'

export interface UserResponse {
  userId: number
  email: string
  role: AppRole
  isActive: boolean
  studentId: number | null
  teacherId: number | null
}

export interface AuthTokenResponse {
  accessToken: string
  accessTokenExpiresAtUtc: string
  refreshToken: string
  refreshTokenExpiresAtUtc: string
  user: UserResponse
}

export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  traceId?: string
  errors?: Record<string, string[]>
}
