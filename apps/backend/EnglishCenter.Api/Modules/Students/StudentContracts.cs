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
    StudentStatus Status,
    byte[] RowVersion);

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
