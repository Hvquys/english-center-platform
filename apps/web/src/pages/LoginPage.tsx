import { type FormEvent, useState } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { ApiError } from '../api/http'
import { useAuth } from '../auth/auth-context'

interface LoginErrors {
  email?: string
  password?: string
}

function validate(email: string, password: string) {
  const errors: LoginErrors = {}
  if (!/^\S+@\S+\.\S+$/.test(email)) errors.email = 'Nhập địa chỉ email hợp lệ.'
  if (password.length < 12) errors.password = 'Mật khẩu cần ít nhất 12 ký tự.'
  return errors
}

function safeReturnPath(value: unknown) {
  return typeof value === 'string' && value.startsWith('/') && !value.startsWith('//')
    ? value
    : '/dashboard'
}

export function LoginPage() {
  const { isAuthenticated, login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [errors, setErrors] = useState<LoginErrors>({})
  const [serverError, setServerError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  if (isAuthenticated) return <Navigate to="/dashboard" replace />

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const nextErrors = validate(email.trim(), password)
    setErrors(nextErrors)
    setServerError('')
    if (Object.keys(nextErrors).length > 0) return

    setIsSubmitting(true)
    try {
      await login(email.trim(), password)
      const state = location.state as { from?: unknown } | null
      navigate(safeReturnPath(state?.from), { replace: true })
    } catch (error) {
      setServerError(
        error instanceof ApiError
          ? error.message
          : 'Không thể kết nối tới API. Hãy kiểm tra dịch vụ backend.',
      )
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <main className="login-page">
      <section className="login-intro" aria-labelledby="login-heading">
        <div className="brand brand-light">
          <span className="brand-mark" aria-hidden="true">EC</span>
          <span><strong>English Center</strong><small>Management Platform</small></span>
        </div>
        <div>
          <p className="eyebrow">Nền tảng quản lý trung tâm</p>
          <h1 id="login-heading">Một nơi để theo dõi toàn bộ hành trình học tập.</h1>
          <p className="login-description">
            Quản lý học viên, lớp học, điểm danh và học phí bằng dữ liệu thống nhất,
            có phân quyền rõ ràng.
          </p>
        </div>
        <p className="security-note">Phiên đăng nhập chỉ được giữ trong tab trình duyệt hiện tại.</p>
      </section>

      <section className="login-panel" aria-label="Đăng nhập">
        <form className="login-card" onSubmit={handleSubmit} noValidate>
          <div className="form-heading">
            <p className="eyebrow">Chào mừng trở lại</p>
            <h2>Đăng nhập</h2>
            <p>Dùng tài khoản do quản trị viên cấp.</p>
          </div>

          <label className="field">
            <span>Email</span>
            <input type="email" value={email} onChange={(event) => setEmail(event.target.value)}
              autoComplete="username" aria-invalid={Boolean(errors.email)}
              aria-describedby={errors.email ? 'email-error' : undefined} placeholder="name@example.com" />
            {errors.email && <small id="email-error" className="field-error">{errors.email}</small>}
          </label>

          <label className="field">
            <span>Mật khẩu</span>
            <input type={showPassword ? 'text' : 'password'} value={password}
              onChange={(event) => setPassword(event.target.value)} autoComplete="current-password"
              aria-invalid={Boolean(errors.password)}
              aria-describedby={errors.password ? 'password-error' : undefined}
              placeholder="Ít nhất 12 ký tự" />
            {errors.password && <small id="password-error" className="field-error">{errors.password}</small>}
          </label>

          <label className="check-row">
            <input type="checkbox" checked={showPassword}
              onChange={(event) => setShowPassword(event.target.checked)} />
            <span>Hiện mật khẩu</span>
          </label>

          {serverError && <div className="alert" role="alert">{serverError}</div>}
          <button className="button button-primary" type="submit" disabled={isSubmitting}>
            {isSubmitting ? 'Đang xác thực…' : 'Đăng nhập'}
          </button>
        </form>
      </section>
    </main>
  )
}
