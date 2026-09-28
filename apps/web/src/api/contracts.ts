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

export interface PagedResponse<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface VersionedResource {
  rowVersion: string
}

export interface StudentResponse extends VersionedResource {
  studentId: number
  studentCode: string
  fullName: string
  dateOfBirth: string | null
  gender: string | null
  email: string | null
  phone: string | null
  guardianName: string | null
  guardianPhone: string | null
  status: string
}

export interface ClassResponse extends VersionedResource {
  classId: number
  classCode: string
  courseId: number
  courseCode: string
  courseName: string
  teacherId: number | null
  teacherName: string | null
  className: string
  startDate: string
  endDate: string
  capacity: number
  scheduleNote: string | null
  roomName: string | null
  status: string
}

export interface EnrollmentResponse extends VersionedResource {
  enrollmentId: number
  studentId: number
  studentCode: string
  studentName: string
  classId: number
  classCode: string
  className: string
  courseCode: string
  agreedTuition: number
  status: string
  completionNote: string | null
}

export interface AttendanceResponse extends VersionedResource {
  attendanceId: number
  enrollmentId: number
  studentCode: string
  studentName: string
  classCode: string
  className: string
  attendanceDate: string
  status: string
  checkInAt: string | null
  note: string | null
  recordedBy: string | null
}

export interface PaymentResponse extends VersionedResource {
  paymentId: number
  enrollmentId: number
  studentCode: string
  studentName: string
  classCode: string
  courseCode: string
  paymentDate: string
  amount: number
  paymentMethod: string
  paymentReference: string | null
  status: string
  note: string | null
  agreedTuition: number
  completedPaid: number
  outstandingAmount: number
}
