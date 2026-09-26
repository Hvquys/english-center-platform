import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/auth-context'

export function AppShell() {
  const { user, logout } = useAuth()

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <span className="brand-mark" aria-hidden="true">EC</span>
          <span>
            <strong>English Center</strong>
            <small>Management Platform</small>
          </span>
        </div>
        <nav aria-label="Điều hướng chính">
          <NavLink to="/dashboard">Tổng quan</NavLink>
          <span className="nav-placeholder" aria-disabled="true">Học viên</span>
          <span className="nav-placeholder" aria-disabled="true">Lớp học</span>
          <span className="nav-placeholder" aria-disabled="true">Thanh toán</span>
        </nav>
        <div className="sidebar-foot">
          <span className="role-badge">{user?.role}</span>
          <span className="user-email" title={user?.email}>{user?.email}</span>
          <button className="button button-ghost" type="button" onClick={() => void logout()}>
            Đăng xuất
          </button>
        </div>
      </aside>
      <main className="main-content">
        <Outlet />
      </main>
    </div>
  )
}
