using System.ComponentModel.DataAnnotations;
using EnglishCenter.Api.Domain.Enums;

namespace EnglishCenter.Api.Modules.Students;

public sealed record StudentResponse(
    long StudentId,
    string StudentCode,
    string FullName,
    DateOnly? DateOfBirth,
    string? Gender,
    string? Email,
    string? Phone,
    string? GuardianName,
    string? GuardianPhone,
    StudentStatus Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string RowVersion);

public sealed class StudentListQuery
{
    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    [StringLength(150)]
    public string? Search { get; init; }

    public StudentStatus? Status { get; init; }
}

public sealed class CreateStudentRequest
{
    [Required]
    [StringLength(20, MinimumLength = 3)]
    [RegularExpression("^[A-Z0-9-]+$", ErrorMessage = "StudentCode may contain uppercase letters, numbers, and hyphens only.")]
    public string? StudentCode { get; init; }

    [Required]
    [StringLength(150, MinimumLength = 2)]
    public string? FullName { get; init; }

    public DateOnly? DateOfBirth { get; init; }

    [RegularExpression("^(MALE|FEMALE|OTHER)$")]
    public string? Gender { get; init; }

    [EmailAddress]
    [StringLength(320)]
    public string? Email { get; init; }

    [StringLength(30)]
    public string? Phone { get; init; }

    [StringLength(150)]
    public string? GuardianName { get; init; }

    [StringLength(30)]
    public string? GuardianPhone { get; init; }
}

public sealed class UpdateStudentRequest
{
    [Required]
    [StringLength(20, MinimumLength = 3)]
    [RegularExpression("^[A-Z0-9-]+$", ErrorMessage = "StudentCode may contain uppercase letters, numbers, and hyphens only.")]
    public string? StudentCode { get; init; }

    [Required]
    [StringLength(150, MinimumLength = 2)]
    public string? FullName { get; init; }

    public DateOnly? DateOfBirth { get; init; }

    [RegularExpression("^(MALE|FEMALE|OTHER)$")]
    public string? Gender { get; init; }

    [EmailAddress]
    [StringLength(320)]
    public string? Email { get; init; }

    [StringLength(30)]
    public string? Phone { get; init; }

    [StringLength(150)]
    public string? GuardianName { get; init; }

    [StringLength(30)]
    public string? GuardianPhone { get; init; }

    [Required]
    public StudentStatus? Status { get; init; }

    [Required]
    public string? RowVersion { get; init; }
}
