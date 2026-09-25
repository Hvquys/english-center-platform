using EnglishCenter.Api.Domain.Enums;

namespace EnglishCenter.Api.Domain.Entities;

public abstract class AuditableEntity
{
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class Student : AuditableEntity
{
    public long StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianPhone { get; set; }
    public StudentStatus Status { get; set; } = StudentStatus.ACTIVE;
    public ICollection<Enrollment> Enrollments { get; set; } = [];
}

public sealed class Teacher : AuditableEntity
{
    public long TeacherId { get; set; }
    public string TeacherCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Specialization { get; set; }
    public TeacherStatus Status { get; set; } = TeacherStatus.ACTIVE;
    public ICollection<Class> Classes { get; set; } = [];
}

public sealed class Course : AuditableEntity
{
    public long CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string LevelCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public short PlannedHours { get; set; }
    public decimal StandardTuition { get; set; }
    public CourseStatus Status { get; set; } = CourseStatus.DRAFT;
    public ICollection<Class> Classes { get; set; } = [];
}

public sealed class Class : AuditableEntity
{
    public long ClassId { get; set; }
    public string ClassCode { get; set; } = string.Empty;
    public long CourseId { get; set; }
    public long? TeacherId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public short Capacity { get; set; }
    public string? ScheduleNote { get; set; }
    public string? RoomName { get; set; }
    public ClassStatus Status { get; set; } = ClassStatus.PLANNED;
    public Course Course { get; set; } = null!;
    public Teacher? Teacher { get; set; }
    public ICollection<Enrollment> Enrollments { get; set; } = [];
}

public sealed class Enrollment : AuditableEntity
{
    public long EnrollmentId { get; set; }
    public long StudentId { get; set; }
    public long ClassId { get; set; }
    public DateTime EnrolledAtUtc { get; set; }
    public decimal AgreedTuition { get; set; }
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.PENDING;
    public string? CompletionNote { get; set; }
    public Student Student { get; set; } = null!;
    public Class Class { get; set; } = null!;
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
}

public sealed class AttendanceRecord : AuditableEntity
{
    public long AttendanceId { get; set; }
    public long EnrollmentId { get; set; }
    public DateOnly AttendanceDate { get; set; }
    public AttendanceStatus Status { get; set; }
    public DateTime? CheckInAt { get; set; }
    public string? Note { get; set; }
    public string? RecordedBy { get; set; }
    public Enrollment Enrollment { get; set; } = null!;
}

public sealed class Payment : AuditableEntity
{
    public long PaymentId { get; set; }
    public long EnrollmentId { get; set; }
    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? PaymentReference { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.PENDING;
    public string? Note { get; set; }
    public Enrollment Enrollment { get; set; } = null!;
}

public sealed class AppUser
{
    public long UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public AppUserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public long? StudentId { get; set; }
    public long? TeacherId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Student? Student { get; set; }
    public Teacher? Teacher { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}

public sealed class RefreshToken
{
    public long RefreshTokenId { get; set; }
    public long UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public AppUser User { get; set; } = null!;
}
