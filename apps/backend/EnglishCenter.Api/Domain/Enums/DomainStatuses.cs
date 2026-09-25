namespace EnglishCenter.Api.Domain.Enums;

public enum AppUserRole
{
    ADMIN,
    STAFF,
    TEACHER,
    STUDENT
}

public enum StudentStatus
{
    ACTIVE,
    INACTIVE,
    GRADUATED,
    SUSPENDED
}

public enum TeacherStatus
{
    ACTIVE,
    INACTIVE,
    ON_LEAVE
}

public enum CourseStatus
{
    DRAFT,
    ACTIVE,
    INACTIVE,
    ARCHIVED
}

public enum ClassStatus
{
    PLANNED,
    OPEN,
    IN_PROGRESS,
    COMPLETED,
    CANCELLED
}

public enum EnrollmentStatus
{
    PENDING,
    ACTIVE,
    COMPLETED,
    CANCELLED,
    WITHDRAWN
}

public enum AttendanceStatus
{
    PRESENT,
    ABSENT,
    LATE,
    EXCUSED
}

public enum PaymentMethod
{
    CASH,
    BANK_TRANSFER,
    CARD,
    E_WALLET,
    OTHER
}

public enum PaymentStatus
{
    PENDING,
    COMPLETED,
    FAILED,
    REFUNDED,
    CANCELLED
}
