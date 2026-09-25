using System.ComponentModel.DataAnnotations;
using EnglishCenter.Api.Domain.Enums;

namespace EnglishCenter.Api.Modules.Courses;

public sealed record CourseResponse(
    long CourseId,
    string CourseCode,
    string CourseName,
    string LevelCode,
    string? Description,
    short PlannedHours,
    decimal StandardTuition,
    CourseStatus Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string RowVersion);

public sealed class CourseListQuery
{
    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    [StringLength(200)]
    public string? Search { get; init; }

    public CourseStatus? Status { get; init; }
}

public sealed class CreateCourseRequest
{
    [Required, StringLength(20, MinimumLength = 3)]
    [RegularExpression("^[A-Z0-9-]+$", ErrorMessage = "CourseCode may contain uppercase letters, numbers, and hyphens only.")]
    public string? CourseCode { get; init; }

    [Required, StringLength(200, MinimumLength = 2)]
    public string? CourseName { get; init; }

    [Required, StringLength(20, MinimumLength = 1)]
    [RegularExpression("^[A-Z0-9-]+$", ErrorMessage = "LevelCode may contain uppercase letters, numbers, and hyphens only.")]
    public string? LevelCode { get; init; }

    [StringLength(1000)]
    public string? Description { get; init; }

    [Required, Range(1, short.MaxValue)]
    public short? PlannedHours { get; init; }

    [Required, Range(typeof(decimal), "0", "999999999999999.9999")]
    public decimal? StandardTuition { get; init; }
}

public sealed class UpdateCourseRequest
{
    [Required, StringLength(20, MinimumLength = 3)]
    [RegularExpression("^[A-Z0-9-]+$", ErrorMessage = "CourseCode may contain uppercase letters, numbers, and hyphens only.")]
    public string? CourseCode { get; init; }

    [Required, StringLength(200, MinimumLength = 2)]
    public string? CourseName { get; init; }

    [Required, StringLength(20, MinimumLength = 1)]
    [RegularExpression("^[A-Z0-9-]+$", ErrorMessage = "LevelCode may contain uppercase letters, numbers, and hyphens only.")]
    public string? LevelCode { get; init; }

    [StringLength(1000)]
    public string? Description { get; init; }

    [Required, Range(1, short.MaxValue)]
    public short? PlannedHours { get; init; }

    [Required, Range(typeof(decimal), "0", "999999999999999.9999")]
    public decimal? StandardTuition { get; init; }

    [Required]
    public CourseStatus? Status { get; init; }

    [Required]
    public string? RowVersion { get; init; }
}
