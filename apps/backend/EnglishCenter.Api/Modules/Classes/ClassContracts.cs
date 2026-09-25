using System.ComponentModel.DataAnnotations;
using EnglishCenter.Api.Domain.Enums;

namespace EnglishCenter.Api.Modules.Classes;

public sealed record ClassResponse(
    long ClassId, string ClassCode, long CourseId, string CourseCode, string CourseName,
    long? TeacherId, string? TeacherCode, string? TeacherName, string ClassName,
    DateOnly StartDate, DateOnly EndDate, short Capacity, string? ScheduleNote, string? RoomName,
    ClassStatus Status, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, string RowVersion);

public sealed class ClassListQuery
{
    [Range(1, 1_000_000)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [StringLength(200)] public string? Search { get; init; }
    public ClassStatus? Status { get; init; }
    [Range(1, long.MaxValue)] public long? CourseId { get; init; }
    [Range(1, long.MaxValue)] public long? TeacherId { get; init; }
}

public sealed class CreateClassRequest
{
    [Required, StringLength(30, MinimumLength = 3)]
    [RegularExpression("^[A-Z0-9-]+$", ErrorMessage = "ClassCode may contain uppercase letters, numbers, and hyphens only.")]
    public string? ClassCode { get; init; }
    [Required, Range(1, long.MaxValue)] public long? CourseId { get; init; }
    [Range(1, long.MaxValue)] public long? TeacherId { get; init; }
    [Required, StringLength(200, MinimumLength = 2)] public string? ClassName { get; init; }
    [Required] public DateOnly? StartDate { get; init; }
    [Required] public DateOnly? EndDate { get; init; }
    [Required, Range(1, short.MaxValue)] public short? Capacity { get; init; }
    [StringLength(500)] public string? ScheduleNote { get; init; }
    [StringLength(100)] public string? RoomName { get; init; }
}

public sealed class UpdateClassRequest
{
    [Required, StringLength(30, MinimumLength = 3)]
    [RegularExpression("^[A-Z0-9-]+$", ErrorMessage = "ClassCode may contain uppercase letters, numbers, and hyphens only.")]
    public string? ClassCode { get; init; }
    [Required, Range(1, long.MaxValue)] public long? CourseId { get; init; }
    [Range(1, long.MaxValue)] public long? TeacherId { get; init; }
    [Required, StringLength(200, MinimumLength = 2)] public string? ClassName { get; init; }
    [Required] public DateOnly? StartDate { get; init; }
    [Required] public DateOnly? EndDate { get; init; }
    [Required, Range(1, short.MaxValue)] public short? Capacity { get; init; }
    [StringLength(500)] public string? ScheduleNote { get; init; }
    [StringLength(100)] public string? RoomName { get; init; }
    [Required] public ClassStatus? Status { get; init; }
    [Required] public string? RowVersion { get; init; }
}
