using System.ComponentModel.DataAnnotations;
using EnglishCenter.Api.Domain.Enums;

namespace EnglishCenter.Api.Modules.Enrollments;

public sealed record EnrollmentResponse(
    long EnrollmentId, long StudentId, string StudentCode, string StudentName,
    long ClassId, string ClassCode, string ClassName, string CourseCode,
    DateTime EnrolledAtUtc, decimal AgreedTuition, EnrollmentStatus Status,
    string? CompletionNote, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, string RowVersion);

public sealed class EnrollmentListQuery
{
    [Range(1, 1_000_000)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [StringLength(200)] public string? Search { get; init; }
    public EnrollmentStatus? Status { get; init; }
    [Range(1, long.MaxValue)] public long? StudentId { get; init; }
    [Range(1, long.MaxValue)] public long? ClassId { get; init; }
}

public sealed class CreateEnrollmentRequest
{
    [Required, Range(1, long.MaxValue)] public long? StudentId { get; init; }
    [Required, Range(1, long.MaxValue)] public long? ClassId { get; init; }
    [Range(typeof(decimal), "0", "999999999999999.9999")] public decimal? AgreedTuition { get; init; }
}

public sealed class UpdateEnrollmentRequest
{
    [Required, Range(typeof(decimal), "0", "999999999999999.9999")] public decimal? AgreedTuition { get; init; }
    [Required] public EnrollmentStatus? Status { get; init; }
    [StringLength(500)] public string? CompletionNote { get; init; }
    [Required] public string? RowVersion { get; init; }
}
