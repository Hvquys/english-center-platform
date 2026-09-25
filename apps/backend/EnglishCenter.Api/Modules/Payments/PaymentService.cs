using System.Data;
using EnglishCenter.Api.Api.Concurrency;
using EnglishCenter.Api.Api.ErrorHandling;
using EnglishCenter.Api.Api.Paging;
using EnglishCenter.Api.Domain.Entities;
using EnglishCenter.Api.Domain.Enums;
using EnglishCenter.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Modules.Payments;

public interface IPaymentService
{
    Task<PagedResponse<PaymentResponse>> GetAllAsync(PaymentListQuery query, CancellationToken ct);
    Task<PaymentResponse> GetByIdAsync(long id, CancellationToken ct);
    Task<PaymentResponse> CreateAsync(CreatePaymentRequest request, CancellationToken ct);
    Task<PaymentResponse> UpdateAsync(long id, UpdatePaymentRequest request, CancellationToken ct);
    Task DeleteAsync(long id, string? rowVersion, CancellationToken ct);
}

internal sealed class PaymentService(EnglishCenterDbContext dbContext) : IPaymentService
{
    public async Task<PagedResponse<PaymentResponse>> GetAllAsync(PaymentListQuery query, CancellationToken ct)
    {
        var payments = BaseQuery(false);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            payments = payments.Where(item => item.Enrollment.Student.StudentCode.Contains(search)
                || item.Enrollment.Student.FullName.Contains(search)
                || item.Enrollment.Class.ClassCode.Contains(search)
                || item.Enrollment.Class.Course.CourseCode.Contains(search)
                || (item.PaymentReference != null && item.PaymentReference.Contains(search)));
        }
        if (query.Status is not null) payments = payments.Where(item => item.Status == query.Status);
        if (query.PaymentMethod is not null) payments = payments.Where(item => item.PaymentMethod == query.PaymentMethod);
        if (query.EnrollmentId is not null) payments = payments.Where(item => item.EnrollmentId == query.EnrollmentId);
        if (query.StudentId is not null) payments = payments.Where(item => item.Enrollment.StudentId == query.StudentId);
        if (query.ClassId is not null) payments = payments.Where(item => item.Enrollment.ClassId == query.ClassId);
        if (query.DateFrom is not null) payments = payments.Where(item => item.PaymentDate >= query.DateFrom);
        if (query.DateTo is not null) payments = payments.Where(item => item.PaymentDate <= query.DateTo);

        var total = await payments.CountAsync(ct);
        var items = await payments.OrderByDescending(item => item.PaymentDate).ThenByDescending(item => item.PaymentId)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
        var enrollmentIds = items.Select(item => item.EnrollmentId).Distinct().ToArray();
        var completedTotals = await dbContext.Payments.AsNoTracking()
            .Where(item => enrollmentIds.Contains(item.EnrollmentId) && item.Status == PaymentStatus.COMPLETED)
            .GroupBy(item => item.EnrollmentId)
            .ToDictionaryAsync(group => group.Key, group => group.Sum(item => item.Amount), ct);
        return new(items.Select(item => ToResponse(item,
                completedTotals.GetValueOrDefault(item.EnrollmentId))).ToArray(),
            query.Page, query.PageSize, total,
            total == 0 ? 0 : (int)Math.Ceiling(total / (double)query.PageSize));
    }

    public async Task<PaymentResponse> GetByIdAsync(long id, CancellationToken ct)
    {
        var item = await FindAsync(id, false, ct);
        return ToResponse(item, await GetCompletedPaidAsync(item.EnrollmentId, ct));
    }

    public async Task<PaymentResponse> CreateAsync(CreatePaymentRequest request, CancellationToken ct)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var enrollment = await FindEnrollmentAsync(request.EnrollmentId!.Value, ct);
        EnsurePayable(enrollment);
        ValidatePaymentDate(request.PaymentDate!.Value);
        var reference = Normalize(request.PaymentReference);
        await EnsureReferenceAvailableAsync(reference, null, ct);
        var item = new Payment
        {
            EnrollmentId = enrollment.EnrollmentId,
            PaymentDate = request.PaymentDate.Value,
            Amount = request.Amount!.Value,
            PaymentMethod = request.PaymentMethod!.Value,
            PaymentReference = reference,
            Status = PaymentStatus.PENDING,
            Note = Normalize(request.Note),
            Enrollment = enrollment
        };
        dbContext.Payments.Add(item);
        await dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToResponse(item, await GetCompletedPaidAsync(item.EnrollmentId, ct));
    }

    public async Task<PaymentResponse> UpdateAsync(long id, UpdatePaymentRequest request, CancellationToken ct)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var item = await FindAsync(id, true, ct);
        var target = request.Status!.Value;
        var paymentDate = request.PaymentDate!.Value;
        var amount = request.Amount!.Value;
        ValidateTransition(item.Status, target);
        ValidatePaymentDate(paymentDate);
        var reference = Normalize(request.PaymentReference);
        await EnsureReferenceAvailableAsync(reference, item.PaymentId, ct);
        if (target == PaymentStatus.COMPLETED)
        {
            var completed = await dbContext.Payments.Where(payment => payment.EnrollmentId == item.EnrollmentId
                && payment.PaymentId != item.PaymentId && payment.Status == PaymentStatus.COMPLETED)
                .SumAsync(payment => (decimal?)payment.Amount, ct) ?? 0m;
            if (completed + amount > item.Enrollment.AgreedTuition)
                throw new ResourceConflictException("Completed payments cannot exceed the agreed tuition.");
        }
        dbContext.Entry(item).Property(value => value.RowVersion).OriginalValue = RowVersionCodec.Decode(request.RowVersion);
        item.PaymentDate = paymentDate;
        item.Amount = amount;
        item.PaymentMethod = request.PaymentMethod!.Value;
        item.PaymentReference = reference;
        item.Status = target;
        item.Note = Normalize(request.Note);
        await dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToResponse(item, await GetCompletedPaidAsync(item.EnrollmentId, ct));
    }

    public async Task DeleteAsync(long id, string? rowVersion, CancellationToken ct)
    {
        var item = await FindAsync(id, true, ct);
        if (item.Status == PaymentStatus.COMPLETED)
            throw new ResourceConflictException("Completed payments must be refunded before they can be deleted.");
        dbContext.Entry(item).Property(value => value.RowVersion).OriginalValue = RowVersionCodec.Decode(rowVersion, "If-Match");
        item.IsDeleted = true;
        await dbContext.SaveChangesAsync(ct);
    }

    private IQueryable<Payment> BaseQuery(bool tracking)
    {
        IQueryable<Payment> query = tracking ? dbContext.Payments : dbContext.Payments.AsNoTracking();
        return query.Include(item => item.Enrollment).ThenInclude(item => item.Student)
            .Include(item => item.Enrollment).ThenInclude(item => item.Class).ThenInclude(item => item.Course);
    }

    private async Task<Payment> FindAsync(long id, bool tracking, CancellationToken ct) =>
        await BaseQuery(tracking).SingleOrDefaultAsync(item => item.PaymentId == id, ct)
        ?? throw new ResourceNotFoundException($"Payment {id} was not found.");

    private async Task<Enrollment> FindEnrollmentAsync(long id, CancellationToken ct) =>
        await dbContext.Enrollments.Include(item => item.Student).Include(item => item.Class).ThenInclude(item => item.Course)
            .SingleOrDefaultAsync(item => item.EnrollmentId == id, ct)
        ?? throw new ResourceNotFoundException($"Enrollment {id} was not found.");

    private static void EnsurePayable(Enrollment enrollment)
    {
        if (enrollment.Status is not (EnrollmentStatus.PENDING or EnrollmentStatus.ACTIVE or EnrollmentStatus.COMPLETED))
            throw new ResourceConflictException("Cancelled or withdrawn enrollments cannot receive payments.");
    }

    private static void ValidatePaymentDate(DateOnly date)
    {
        if (date > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new RequestValidationException("PaymentDate cannot be in the future.",
                new Dictionary<string, string[]> { ["PaymentDate"] = ["PaymentDate cannot be in the future."] });
    }

    private async Task EnsureReferenceAvailableAsync(string? reference, long? currentId, CancellationToken ct)
    {
        if (reference is null) return;
        if (await dbContext.Payments.IgnoreQueryFilters().AnyAsync(
            item => item.PaymentReference == reference && item.PaymentId != currentId, ct))
            throw new ResourceConflictException("PaymentReference is already in use.");
    }

    private static void ValidateTransition(PaymentStatus current, PaymentStatus target)
    {
        if (current == target) return;
        var allowed = current switch
        {
            PaymentStatus.PENDING => target is PaymentStatus.COMPLETED or PaymentStatus.FAILED or PaymentStatus.CANCELLED,
            PaymentStatus.COMPLETED => target == PaymentStatus.REFUNDED,
            _ => false
        };
        if (!allowed) throw new ResourceConflictException($"Payment status cannot change from {current} to {target}.");
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<decimal> GetCompletedPaidAsync(long enrollmentId, CancellationToken ct) =>
        await dbContext.Payments.AsNoTracking()
            .Where(item => item.EnrollmentId == enrollmentId && item.Status == PaymentStatus.COMPLETED)
            .SumAsync(item => (decimal?)item.Amount, ct) ?? 0m;

    private static PaymentResponse ToResponse(Payment item, decimal completedPaid)
    {
        return new(item.PaymentId, item.EnrollmentId, item.Enrollment.StudentId,
            item.Enrollment.Student.StudentCode, item.Enrollment.Student.FullName, item.Enrollment.ClassId,
            item.Enrollment.Class.ClassCode, item.Enrollment.Class.Course.CourseCode, item.PaymentDate,
            item.Amount, item.PaymentMethod, item.PaymentReference, item.Status, item.Note,
            item.Enrollment.AgreedTuition, completedPaid, Math.Max(0m, item.Enrollment.AgreedTuition - completedPaid),
            item.CreatedAtUtc, item.UpdatedAtUtc, RowVersionCodec.Encode(item.RowVersion));
    }
}
