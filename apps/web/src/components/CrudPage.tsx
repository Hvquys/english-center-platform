import { type FormEvent, useCallback, useEffect, useMemo, useState } from 'react'
import type { AppRole, PagedResponse, VersionedResource } from '../api/contracts'
import { ApiError } from '../api/http'
import { useAuth } from '../auth/auth-context'

type Resource = VersionedResource & Record<string, unknown>
type InputKind = 'text' | 'email' | 'number' | 'date' | 'datetime-local' | 'select' | 'textarea'

export interface Column {
  key: string
  label: string
  format?: 'date' | 'datetime' | 'currency' | 'status'
}

export interface FieldOption { value: string; label: string }

export interface Field {
  key: string
  label: string
  kind?: InputKind
  required?: boolean
  options?: FieldOption[]
  optionsEndpoint?: string
  optionValueKey?: string
  optionLabelKeys?: string[]
  min?: number
  step?: string
  createOnly?: boolean
  updateOnly?: boolean
}

export interface CrudConfig {
  title: string
  eyebrow: string
  description: string
  endpoint: string
  idKey: string
  noun: string
  columns: Column[]
  fields: Field[]
  readRoles: AppRole[]
  writeRoles: AppRole[]
  statusOptions?: FieldOption[]
}

const statusLabels: Record<string, string> = {
  ACTIVE: 'Đang hoạt động', INACTIVE: 'Ngừng hoạt động', GRADUATED: 'Đã tốt nghiệp', SUSPENDED: 'Tạm đình chỉ',
  PLANNED: 'Dự kiến', OPEN: 'Đang mở', IN_PROGRESS: 'Đang học', COMPLETED: 'Hoàn tất', CANCELLED: 'Đã hủy',
  PENDING: 'Chờ xử lý', WITHDRAWN: 'Đã rút', PRESENT: 'Có mặt', ABSENT: 'Vắng', LATE: 'Đi muộn', EXCUSED: 'Có phép',
  FAILED: 'Thất bại', REFUNDED: 'Đã hoàn tiền', CASH: 'Tiền mặt', BANK_TRANSFER: 'Chuyển khoản', CARD: 'Thẻ',
  E_WALLET: 'Ví điện tử', OTHER: 'Khác', DRAFT: 'Bản nháp', ARCHIVED: 'Lưu trữ', ON_LEAVE: 'Nghỉ phép',
}

function errorMessage(error: unknown) {
  if (!(error instanceof ApiError)) return 'Có lỗi không xác định. Vui lòng thử lại.'
  const details = error.problem.errors ? Object.values(error.problem.errors).flat().join(' ') : ''
  return details || error.message
}

function displayValue(value: unknown, format?: Column['format']) {
  if (value === null || value === undefined || value === '') return '—'
  if (format === 'currency') return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(Number(value))
  if (format === 'date') return new Intl.DateTimeFormat('vi-VN').format(new Date(`${String(value)}T00:00:00`))
  if (format === 'datetime') return new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(String(value)))
  if (format === 'status') return statusLabels[String(value)] ?? String(value)
  return String(value)
}

function initialForm(fields: Field[]) {
  return Object.fromEntries(fields.map((field) => [field.key, ''])) as Record<string, string>
}

export function CrudPage({ config }: { config: CrudConfig }) {
  const { user, request } = useAuth()
  const [items, setItems] = useState<Resource[]>([])
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [totalPages, setTotalPages] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [loading, setLoading] = useState(true)
  const [notice, setNotice] = useState('')
  const [error, setError] = useState('')
  const [editing, setEditing] = useState<Resource | null | undefined>(undefined)
  const [form, setForm] = useState<Record<string, string>>(() => initialForm(config.fields))
  const [fieldOptions, setFieldOptions] = useState<Record<string, FieldOption[]>>({})
  const [saving, setSaving] = useState(false)

  const canRead = Boolean(user && config.readRoles.includes(user.role))
  const canWrite = Boolean(user && config.writeRoles.includes(user.role))

  const load = useCallback(async () => {
    if (!canRead) return
    setLoading(true); setError('')
    const query = new URLSearchParams({ page: String(page), pageSize: '20' })
    if (search.trim()) query.set('search', search.trim())
    if (status) query.set('status', status)
    try {
      const result = await request<PagedResponse<Resource>>(`${config.endpoint}?${query}`)
      setItems(result.items); setTotalPages(Math.max(result.totalPages, 1)); setTotalCount(result.totalCount)
    } catch (caught) { setError(errorMessage(caught)) }
    finally { setLoading(false) }
  }, [canRead, config.endpoint, page, request, search, status])

  useEffect(() => {
    // Fetching is the external synchronization performed by this effect.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load()
  }, [load])

  const optionFields = useMemo(() => config.fields.filter((field) => field.optionsEndpoint), [config.fields])
  useEffect(() => {
    if (!canWrite || optionFields.length === 0) return
    void Promise.all(optionFields.map(async (field) => {
      const result = await request<PagedResponse<Resource>>(`${field.optionsEndpoint}?pageSize=100`)
      const options = result.items.map((item) => ({
        value: String(item[field.optionValueKey ?? 'id']),
        label: (field.optionLabelKeys ?? []).map((key) => item[key]).filter(Boolean).join(' · '),
      }))
      return [field.key, options] as const
    })).then((entries) => setFieldOptions(Object.fromEntries(entries))).catch((caught) => setError(errorMessage(caught)))
  }, [canWrite, optionFields, request])

  function openCreate() { setForm(initialForm(config.fields)); setEditing(null); setError(''); setNotice('') }
  function openEdit(item: Resource) {
    setForm(Object.fromEntries(config.fields.map((field) => [field.key, item[field.key] == null ? '' : String(item[field.key])])))
    setEditing(item); setError(''); setNotice('')
  }

  function payloadForSubmit() {
    const activeFields = config.fields.filter((field) => editing ? !field.createOnly : !field.updateOnly)
    const body: Record<string, unknown> = {}
    for (const field of activeFields) {
      const raw = form[field.key]?.trim() ?? ''
      body[field.key] = field.kind === 'number' ? (raw ? Number(raw) : null) : (raw || null)
      if (field.kind === 'datetime-local' && raw) body[field.key] = new Date(raw).toISOString()
    }
    if (editing) body.rowVersion = editing.rowVersion
    return body
  }

  async function submit(event: FormEvent) {
    event.preventDefault(); setSaving(true); setError(''); setNotice('')
    try {
      const id = editing?.[config.idKey]
      await request(editing ? `${config.endpoint}/${id}` : config.endpoint, {
        method: editing ? 'PUT' : 'POST', body: payloadForSubmit(),
      })
      setEditing(undefined); setNotice(`${editing ? 'Đã cập nhật' : 'Đã thêm'} ${config.noun}.`); await load()
    } catch (caught) { setError(errorMessage(caught)) }
    finally { setSaving(false) }
  }

  async function remove(item: Resource) {
    if (!window.confirm(`Xóa ${config.noun} này? Dữ liệu liên quan có thể khiến thao tác bị từ chối.`)) return
    setError(''); setNotice('')
    try {
      await request(`${config.endpoint}/${item[config.idKey]}`, { method: 'DELETE', headers: { 'If-Match': `"${item.rowVersion}"` } })
      setNotice(`Đã xóa ${config.noun}.`); await load()
    } catch (caught) { setError(errorMessage(caught)) }
  }

  if (!canRead) return <div className="resource-page"><section className="content-card empty-state"><h1>Không có quyền truy cập</h1><p>Tài khoản {user?.role} không được backend cấp quyền cho khu vực này.</p></section></div>

  return <div className="resource-page">
    <header className="page-header"><div><p className="eyebrow">{config.eyebrow}</p><h1>{config.title}</h1><p>{config.description}</p></div>
      {canWrite && <button className="button button-primary" type="button" onClick={openCreate}>+ Thêm {config.noun}</button>}
    </header>

    <section className="content-card resource-card">
      <form className="toolbar" onSubmit={(event) => { event.preventDefault(); setPage(1); void load() }}>
        <label><span className="sr-only">Tìm kiếm</span><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder={`Tìm ${config.noun}…`} /></label>
        {config.statusOptions && <label><span className="sr-only">Trạng thái</span><select value={status} onChange={(event) => { setStatus(event.target.value); setPage(1) }}><option value="">Tất cả trạng thái</option>{config.statusOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}</select></label>}
        <button className="button button-secondary" type="submit">Tìm kiếm</button>
        <span className="result-count">{totalCount} kết quả</span>
      </form>
      {error && <div className="alert" role="alert">{error}</div>}
      {notice && <div className="success-alert" role="status">{notice}</div>}
      <div className="table-wrap"><table><thead><tr>{config.columns.map((column) => <th key={column.key}>{column.label}</th>)}{canWrite && <th>Thao tác</th>}</tr></thead>
        <tbody>{loading ? <tr><td colSpan={config.columns.length + 1}>Đang tải dữ liệu…</td></tr> : items.length === 0 ? <tr><td className="empty-cell" colSpan={config.columns.length + 1}>Chưa có dữ liệu phù hợp.</td></tr> : items.map((item) => <tr key={String(item[config.idKey])}>{config.columns.map((column) => <td key={column.key} data-label={column.label}>{column.format === 'status' ? <span className={`data-status status-${String(item[column.key]).toLowerCase()}`}>{displayValue(item[column.key], column.format)}</span> : displayValue(item[column.key], column.format)}</td>)}{canWrite && <td data-label="Thao tác"><div className="row-actions"><button type="button" onClick={() => openEdit(item)}>Sửa</button><button className="danger-link" type="button" onClick={() => void remove(item)}>Xóa</button></div></td>}</tr>)}</tbody>
      </table></div>
      <div className="pagination"><button type="button" disabled={page <= 1} onClick={() => setPage((value) => value - 1)}>← Trước</button><span>Trang {page}/{totalPages}</span><button type="button" disabled={page >= totalPages} onClick={() => setPage((value) => value + 1)}>Sau →</button></div>
    </section>

    {editing !== undefined && <div className="modal-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) setEditing(undefined) }}><section className="modal" role="dialog" aria-modal="true" aria-labelledby="resource-form-title"><div className="modal-heading"><div><p className="eyebrow">{editing ? 'Cập nhật dữ liệu' : 'Tạo dữ liệu mới'}</p><h2 id="resource-form-title">{editing ? `Sửa ${config.noun}` : `Thêm ${config.noun}`}</h2></div><button className="close-button" type="button" aria-label="Đóng" onClick={() => setEditing(undefined)}>×</button></div>
      <form onSubmit={(event) => void submit(event)}><div className="form-grid">{config.fields.filter((field) => editing ? !field.createOnly : !field.updateOnly).map((field) => { const options = field.options ?? fieldOptions[field.key]; return <label className={`field ${field.kind === 'textarea' ? 'field-wide' : ''}`} key={field.key}>{field.label}{options ? <select required={field.required} value={form[field.key] ?? ''} onChange={(event) => setForm((value) => ({ ...value, [field.key]: event.target.value }))}><option value="">Chọn…</option>{options.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}</select> : field.kind === 'textarea' ? <textarea required={field.required} value={form[field.key] ?? ''} onChange={(event) => setForm((value) => ({ ...value, [field.key]: event.target.value }))} /> : <input type={field.kind ?? 'text'} required={field.required} min={field.min} step={field.step} value={form[field.key] ?? ''} onChange={(event) => setForm((value) => ({ ...value, [field.key]: event.target.value }))} />}</label>})}</div>
        {error && <div className="alert" role="alert">{error}</div>}<div className="modal-actions"><button className="button button-secondary" type="button" onClick={() => setEditing(undefined)}>Hủy</button><button className="button button-primary" type="submit" disabled={saving}>{saving ? 'Đang lưu…' : 'Lưu dữ liệu'}</button></div></form>
    </section></div>}
  </div>
}
