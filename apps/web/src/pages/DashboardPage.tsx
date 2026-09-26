import { useState } from 'react'
import type { UserResponse } from '../api/contracts'
import { ApiError } from '../api/http'
import { useAuth } from '../auth/auth-context'

const modules = [
  ['Học viên', 'Hồ sơ và trạng thái học tập', 'Sẵn sàng'],
  ['Lớp học', 'Khóa học, lớp và ghi danh', 'Sẵn sàng'],
  ['Điểm danh', 'Theo dõi từng buổi học', 'Sẵn sàng'],
  ['Học phí', 'Thanh toán và số dư', 'Sẵn sàng'],
]

export function DashboardPage() {
  const { user, request } = useAuth()
  const [sessionStatus, setSessionStatus] = useState('Chưa kiểm tra trong phiên này.')
  const [isChecking, setIsChecking] = useState(false)

  async function checkSession() {
    setIsChecking(true)
    try {
      const currentUser = await request<UserResponse>('/auth/me')
      setSessionStatus(`API xác nhận ${currentUser.email} với vai trò ${currentUser.role}.`)
    } catch (error) {
      setSessionStatus(error instanceof ApiError ? error.message : 'Không thể kiểm tra API.')
    } finally {
      setIsChecking(false)
    }
  }

  return (
    <div className="dashboard-page">
      <header className="page-header">
        <div>
          <p className="eyebrow">Tổng quan hệ thống</p>
          <h1>Xin chào, {user?.email.split('@')[0]}</h1>
          <p>Backend nghiệp vụ đã sẵn sàng. Giao diện quản lý sẽ được nối theo từng module.</p>
        </div>
        <span className="status-pill"><span aria-hidden="true" /> API đã bảo vệ</span>
      </header>

      <section className="stat-grid" aria-label="Trạng thái nền tảng">
        <article className="stat-card featured"><span className="stat-label">Vai trò hiện tại</span>
          <strong>{user?.role}</strong><small>Quyền trên API được kiểm tra phía máy chủ.</small></article>
        <article className="stat-card"><span className="stat-label">Module nghiệp vụ</span>
          <strong>7</strong><small>Student đến Payment đã có API.</small></article>
        <article className="stat-card"><span className="stat-label">Bảo mật</span>
          <strong>JWT + RBAC</strong><small>Refresh rotation và revoke khi đăng xuất.</small></article>
      </section>

      <section className="content-card">
        <div className="section-heading"><div><p className="eyebrow">Application Core</p>
          <h2>Các khu vực quản lý</h2></div><span>Giao diện chi tiết thuộc TASK-011</span></div>
        <div className="module-grid">
          {modules.map(([name, description, status]) => (
            <article className="module-card" key={name}>
              <div className="module-icon" aria-hidden="true">{name.slice(0, 1)}</div>
              <div><h3>{name}</h3><p>{description}</p></div><span>{status}</span>
            </article>
          ))}
        </div>
      </section>

      <section className="content-card session-card">
        <div><p className="eyebrow">Kiểm tra kết nối</p><h2>Phiên đăng nhập</h2>
          <p>{sessionStatus}</p></div>
        <button className="button button-secondary" type="button"
          onClick={() => void checkSession()} disabled={isChecking}>
          {isChecking ? 'Đang kiểm tra…' : 'Gọi /api/auth/me'}
        </button>
      </section>
    </div>
  )
}
