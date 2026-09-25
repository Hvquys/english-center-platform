using System.ComponentModel.DataAnnotations;
using EnglishCenter.Api.Domain.Enums;

namespace EnglishCenter.Api.Modules.Attendance;

public sealed record AttendanceResponse(
    long AttendanceId, long EnrollmentId, long StudentId, string StudentCode, string StudentName,
    long ClassId, string ClassCode, string ClassName, DateOnly AttendanceDate, AttendanceStatus Status,
    DateTime? CheckInAt, string? Note, string? RecordedBy,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc, string RowVersion);

public sealed class AttendanceListQuery : IValidatableObject
{
    [Range(1, 1_000_000)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [StringLength(200)] public string? Search { get; init; }
    public AttendanceStatus? Status { get; init; }
    [Range(1, long.MaxValue)] public long? EnrollmentId { get; init; }
    [Range(1, long.MaxValue)] public long? StudentId { get; init; }
    [Range(1, long.MaxValue)] public long? ClassId { get; init; }
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DateFrom is not null && DateTo is not null && DateTo < DateFrom)
            yield return new ValidationResult("DateTo cannot be before DateFrom.", [nameof(DateTo)]);
    }
}

public sealed class CreateAttendanceRequest
{
    [Required, Range(1, long.MaxValue)] public long? EnrollmentId { get; init; }
    [Required] public DateOnly? AttendanceDate { get; init; }
    [Required] public AttendanceStatus? Status { get; init; }
    public DateTime? CheckInAt { get; init; }
    [StringLength(500)] public string? Note { get; init; }
    [StringLength(100)] public string? RecordedBy { get; init; }
}

public sealed class UpdateAttendanceRequest
{
    [Required] public AttendanceStatus? Status { get; init; }
    public DateTime? CheckInAt { get; init; }
    [StringLength(500)] public string? Note { get; init; }
    [StringLength(100)] public string? RecordedBy { get; init; }
    [Required] public string? RowVersion { get; init; }
}
