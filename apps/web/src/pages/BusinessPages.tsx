import { CrudPage, type CrudConfig, type FieldOption } from '../components/CrudPage'

const staff = ['ADMIN', 'STAFF'] as const
const teaching = ['ADMIN', 'STAFF', 'TEACHER'] as const
const all = ['ADMIN', 'STAFF', 'TEACHER', 'STUDENT'] as const
const optionLabels: Record<string, string> = {
  ACTIVE: 'Đang hoạt động', INACTIVE: 'Ngừng hoạt động', GRADUATED: 'Đã tốt nghiệp', SUSPENDED: 'Tạm đình chỉ',
  MALE: 'Nam', FEMALE: 'Nữ', OTHER: 'Khác', PLANNED: 'Dự kiến', OPEN: 'Đang mở', IN_PROGRESS: 'Đang học',
  COMPLETED: 'Hoàn tất', CANCELLED: 'Đã hủy', PENDING: 'Chờ xử lý', WITHDRAWN: 'Đã rút', PRESENT: 'Có mặt',
  ABSENT: 'Vắng', LATE: 'Đi muộn', EXCUSED: 'Có phép', FAILED: 'Thất bại', REFUNDED: 'Đã hoàn tiền',
  CASH: 'Tiền mặt', BANK_TRANSFER: 'Chuyển khoản', CARD: 'Thẻ', E_WALLET: 'Ví điện tử',
}
const options = (values: string[]): FieldOption[] => values.map((value) => ({ value, label: optionLabels[value] ?? value.replaceAll('_', ' ') }))

const studentConfig: CrudConfig = {
  title: 'Quản lý học viên', eyebrow: 'Student Management', description: 'Lưu hồ sơ, liên hệ và trạng thái học tập của học viên.', endpoint: '/students', idKey: 'studentId', noun: 'học viên', readRoles: [...staff], writeRoles: [...staff],
  statusOptions: options(['ACTIVE', 'INACTIVE', 'GRADUATED', 'SUSPENDED']),
  columns: [{ key: 'studentCode', label: 'Mã' }, { key: 'fullName', label: 'Họ tên' }, { key: 'email', label: 'Email' }, { key: 'phone', label: 'Điện thoại' }, { key: 'status', label: 'Trạng thái', format: 'status' }],
  fields: [{ key: 'studentCode', label: 'Mã học viên', required: true }, { key: 'fullName', label: 'Họ và tên', required: true }, { key: 'dateOfBirth', label: 'Ngày sinh', kind: 'date' }, { key: 'gender', label: 'Giới tính', kind: 'select', options: options(['MALE', 'FEMALE', 'OTHER']) }, { key: 'email', label: 'Email', kind: 'email' }, { key: 'phone', label: 'Điện thoại' }, { key: 'guardianName', label: 'Người giám hộ' }, { key: 'guardianPhone', label: 'SĐT người giám hộ' }, { key: 'status', label: 'Trạng thái', kind: 'select', options: options(['ACTIVE', 'INACTIVE', 'GRADUATED', 'SUSPENDED']), required: true, updateOnly: true }],
}

const classConfig: CrudConfig = {
  title: 'Quản lý lớp học', eyebrow: 'Class Management', description: 'Theo dõi khóa học, giáo viên, lịch và sức chứa từng lớp.', endpoint: '/classes', idKey: 'classId', noun: 'lớp học', readRoles: [...all], writeRoles: [...staff],
  statusOptions: options(['PLANNED', 'OPEN', 'IN_PROGRESS', 'COMPLETED', 'CANCELLED']),
  columns: [{ key: 'classCode', label: 'Mã lớp' }, { key: 'className', label: 'Tên lớp' }, { key: 'courseName', label: 'Khóa học' }, { key: 'teacherName', label: 'Giáo viên' }, { key: 'startDate', label: 'Bắt đầu', format: 'date' }, { key: 'status', label: 'Trạng thái', format: 'status' }],
  fields: [{ key: 'classCode', label: 'Mã lớp', required: true }, { key: 'className', label: 'Tên lớp', required: true }, { key: 'courseId', label: 'Khóa học', kind: 'select', required: true, optionsEndpoint: '/courses', optionValueKey: 'courseId', optionLabelKeys: ['courseCode', 'courseName'] }, { key: 'teacherId', label: 'Giáo viên', kind: 'select', optionsEndpoint: '/teachers', optionValueKey: 'teacherId', optionLabelKeys: ['teacherCode', 'fullName'] }, { key: 'startDate', label: 'Ngày bắt đầu', kind: 'date', required: true }, { key: 'endDate', label: 'Ngày kết thúc', kind: 'date', required: true }, { key: 'capacity', label: 'Sức chứa', kind: 'number', min: 1, required: true }, { key: 'roomName', label: 'Phòng học' }, { key: 'scheduleNote', label: 'Lịch học', kind: 'textarea' }, { key: 'status', label: 'Trạng thái', kind: 'select', options: options(['PLANNED', 'OPEN', 'IN_PROGRESS', 'COMPLETED', 'CANCELLED']), required: true, updateOnly: true }],
}

const enrollmentConfig: CrudConfig = {
  title: 'Quản lý ghi danh', eyebrow: 'Enrollment Management', description: 'Ghép học viên với lớp và theo dõi học phí đã thỏa thuận.', endpoint: '/enrollments', idKey: 'enrollmentId', noun: 'ghi danh', readRoles: [...staff], writeRoles: [...staff],
  statusOptions: options(['PENDING', 'ACTIVE', 'COMPLETED', 'CANCELLED', 'WITHDRAWN']),
  columns: [{ key: 'studentName', label: 'Học viên' }, { key: 'className', label: 'Lớp học' }, { key: 'courseCode', label: 'Khóa' }, { key: 'agreedTuition', label: 'Học phí', format: 'currency' }, { key: 'status', label: 'Trạng thái', format: 'status' }],
  fields: [{ key: 'studentId', label: 'Học viên', kind: 'select', required: true, createOnly: true, optionsEndpoint: '/students', optionValueKey: 'studentId', optionLabelKeys: ['studentCode', 'fullName'] }, { key: 'classId', label: 'Lớp học', kind: 'select', required: true, createOnly: true, optionsEndpoint: '/classes', optionValueKey: 'classId', optionLabelKeys: ['classCode', 'className'] }, { key: 'agreedTuition', label: 'Học phí thỏa thuận', kind: 'number', min: 0, step: '1000', required: true }, { key: 'status', label: 'Trạng thái', kind: 'select', options: options(['PENDING', 'ACTIVE', 'COMPLETED', 'CANCELLED', 'WITHDRAWN']), required: true, updateOnly: true }, { key: 'completionNote', label: 'Ghi chú hoàn tất', kind: 'textarea', updateOnly: true }],
}

const attendanceConfig: CrudConfig = {
  title: 'Quản lý điểm danh', eyebrow: 'Attendance Management', description: 'Ghi nhận có mặt, vắng, đi muộn hoặc nghỉ có phép theo ngày.', endpoint: '/attendance', idKey: 'attendanceId', noun: 'điểm danh', readRoles: [...teaching], writeRoles: [...teaching],
  statusOptions: options(['PRESENT', 'ABSENT', 'LATE', 'EXCUSED']),
  columns: [{ key: 'attendanceDate', label: 'Ngày', format: 'date' }, { key: 'studentName', label: 'Học viên' }, { key: 'className', label: 'Lớp học' }, { key: 'status', label: 'Trạng thái', format: 'status' }, { key: 'recordedBy', label: 'Người ghi' }],
  fields: [{ key: 'enrollmentId', label: 'Mã ghi danh', kind: 'number', min: 1, required: true, createOnly: true }, { key: 'attendanceDate', label: 'Ngày điểm danh', kind: 'date', required: true, createOnly: true }, { key: 'status', label: 'Trạng thái', kind: 'select', required: true, options: options(['PRESENT', 'ABSENT', 'LATE', 'EXCUSED']) }, { key: 'checkInAt', label: 'Thời điểm vào lớp', kind: 'datetime-local' }, { key: 'recordedBy', label: 'Người ghi nhận' }, { key: 'note', label: 'Ghi chú', kind: 'textarea' }],
}

const paymentConfig: CrudConfig = {
  title: 'Quản lý thanh toán', eyebrow: 'Payment Management', description: 'Ghi nhận học phí, phương thức và số tiền còn phải thu.', endpoint: '/payments', idKey: 'paymentId', noun: 'thanh toán', readRoles: [...staff], writeRoles: [...staff],
  statusOptions: options(['PENDING', 'COMPLETED', 'FAILED', 'REFUNDED', 'CANCELLED']),
  columns: [{ key: 'paymentDate', label: 'Ngày', format: 'date' }, { key: 'studentName', label: 'Học viên' }, { key: 'amount', label: 'Số tiền', format: 'currency' }, { key: 'paymentMethod', label: 'Phương thức', format: 'status' }, { key: 'status', label: 'Trạng thái', format: 'status' }, { key: 'outstandingAmount', label: 'Còn lại', format: 'currency' }],
  fields: [{ key: 'enrollmentId', label: 'Ghi danh', kind: 'select', required: true, createOnly: true, optionsEndpoint: '/enrollments', optionValueKey: 'enrollmentId', optionLabelKeys: ['studentCode', 'studentName', 'classCode'] }, { key: 'paymentDate', label: 'Ngày thanh toán', kind: 'date', required: true }, { key: 'amount', label: 'Số tiền', kind: 'number', min: 1, step: '1000', required: true }, { key: 'paymentMethod', label: 'Phương thức', kind: 'select', required: true, options: options(['CASH', 'BANK_TRANSFER', 'CARD', 'E_WALLET', 'OTHER']) }, { key: 'paymentReference', label: 'Mã tham chiếu' }, { key: 'status', label: 'Trạng thái', kind: 'select', options: options(['PENDING', 'COMPLETED', 'FAILED', 'REFUNDED', 'CANCELLED']), required: true, updateOnly: true }, { key: 'note', label: 'Ghi chú', kind: 'textarea' }],
}

export const StudentsPage = () => <CrudPage config={studentConfig} />
export const ClassesPage = () => <CrudPage config={classConfig} />
export const EnrollmentsPage = () => <CrudPage config={enrollmentConfig} />
export const AttendancePage = () => <CrudPage config={attendanceConfig} />
export const PaymentsPage = () => <CrudPage config={paymentConfig} />

