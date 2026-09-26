import { Link } from 'react-router-dom'

export function NotFoundPage() {
  return (
    <main className="not-found-page">
      <p className="eyebrow">404</p>
      <h1>Không tìm thấy trang</h1>
      <p>Đường dẫn này chưa tồn tại trong English Center Platform.</p>
      <Link className="button button-primary" to="/dashboard">Về trang tổng quan</Link>
    </main>
  )
}
