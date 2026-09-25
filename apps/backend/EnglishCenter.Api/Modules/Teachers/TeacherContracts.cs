using System.ComponentModel.DataAnnotations;
using EnglishCenter.Api.Domain.Enums;

namespace EnglishCenter.Api.Modules.Teachers;

public sealed record TeacherResponse(
    long TeacherId,
    string TeacherCode,
    string FullName,
    string? Email,
    string? Phone,
    string? Specialization,
    TeacherStatus Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string RowVersion);

public sealed class TeacherListQuery
{
    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    [StringLength(150)]
    public string? Search { get; init; }

    public TeacherStatus? Status { get; init; }
}

public sealed class CreateTeacherRequest
{
    [Required]
    [StringLength(20, MinimumLength = 3)]
    [RegularExpression("^[A-Z0-9-]+$", ErrorMessage = "TeacherCode may contain uppercase letters, numbers, and hyphens only.")]
    public string? TeacherCode { get; init; }

    [Required]
    [StringLength(150, MinimumLength = 2)]
    public string? FullName { get; init; }

    [EmailAddress]
    [StringLength(320)]
    public string? Email { get; init; }

    [StringLength(30)]
    public string? Phone { get; init; }

    [StringLength(200)]
    public string? Specialization { get; init; }
}

public sealed class UpdateTeacherRequest
{
    [Required]
    [StringLength(20, MinimumLength = 3)]
    [RegularExpression("^[A-Z0-9-]+$", ErrorMessage = "TeacherCode may contain uppercase letters, numbers, and hyphens only.")]
    public string? TeacherCode { get; init; }

    [Required]
    [StringLength(150, MinimumLength = 2)]
    public string? FullName { get; init; }

    [EmailAddress]
    [StringLength(320)]
    public string? Email { get; init; }

    [StringLength(30)]
    public string? Phone { get; init; }

    [StringLength(200)]
    public string? Specialization { get; init; }

    [Required]
    public TeacherStatus? Status { get; init; }

    [Required]
    public string? RowVersion { get; init; }
}
