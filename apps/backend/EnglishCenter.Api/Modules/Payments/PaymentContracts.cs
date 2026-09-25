using System.ComponentModel.DataAnnotations;
using EnglishCenter.Api.Domain.Enums;

namespace EnglishCenter.Api.Modules.Payments;

public sealed record PaymentResponse(
    long PaymentId, long EnrollmentId, long StudentId, string StudentCode, string StudentName,
    long ClassId, string ClassCode, string CourseCode, DateOnly PaymentDate, decimal Amount,
    PaymentMethod PaymentMethod, string? PaymentReference, PaymentStatus Status, string? Note,
    decimal AgreedTuition, decimal CompletedPaid, decimal OutstandingAmount,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc, string RowVersion);

public sealed class PaymentListQuery : IValidatableObject
{
    [Range(1, 1_000_000)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [StringLength(200)] public string? Search { get; init; }
    public PaymentStatus? Status { get; init; }
    public PaymentMethod? PaymentMethod { get; init; }
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

public sealed class CreatePaymentRequest
{
    [Required, Range(1, long.MaxValue)] public long? EnrollmentId { get; init; }
    [Required] public DateOnly? PaymentDate { get; init; }
    [Required, Range(typeof(decimal), "0.0001", "999999999999999.9999")] public decimal? Amount { get; init; }
    [Required] public PaymentMethod? PaymentMethod { get; init; }
    [StringLength(100)] public string? PaymentReference { get; init; }
    [StringLength(500)] public string? Note { get; init; }
}

public sealed class UpdatePaymentRequest
{
    [Required] public DateOnly? PaymentDate { get; init; }
    [Required, Range(typeof(decimal), "0.0001", "999999999999999.9999")] public decimal? Amount { get; init; }
    [Required] public PaymentMethod? PaymentMethod { get; init; }
    [StringLength(100)] public string? PaymentReference { get; init; }
    [Required] public PaymentStatus? Status { get; init; }
    [StringLength(500)] public string? Note { get; init; }
    [Required] public string? RowVersion { get; init; }
}
