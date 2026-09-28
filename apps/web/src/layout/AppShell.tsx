import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/auth-context'

export function AppShell() {
  const { user, logout } = useAuth()
  const isStaff = user?.role === 'ADMIN' || user?.role === 'STAFF'
  const canTeach = isStaff || user?.role === 'TEACHER'

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
          {isStaff && <NavLink to="/students">Học viên</NavLink>}
          <NavLink to="/classes">Lớp học</NavLink>
          {isStaff && <NavLink to="/enrollments">Ghi danh</NavLink>}
          {canTeach && <NavLink to="/attendance">Điểm danh</NavLink>}
          {isStaff && <NavLink to="/payments">Thanh toán</NavLink>}
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
