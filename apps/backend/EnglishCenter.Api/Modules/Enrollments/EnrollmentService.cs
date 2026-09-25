using System.Data;
using EnglishCenter.Api.Api.Concurrency;
using EnglishCenter.Api.Api.ErrorHandling;
using EnglishCenter.Api.Api.Paging;
using EnglishCenter.Api.Domain.Entities;
using EnglishCenter.Api.Domain.Enums;
using EnglishCenter.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Modules.Enrollments;

public interface IEnrollmentService
{
    Task<PagedResponse<EnrollmentResponse>> GetAllAsync(EnrollmentListQuery query, CancellationToken ct);
    Task<EnrollmentResponse> GetByIdAsync(long id, CancellationToken ct);
    Task<EnrollmentResponse> CreateAsync(CreateEnrollmentRequest request, CancellationToken ct);
    Task<EnrollmentResponse> UpdateAsync(long id, UpdateEnrollmentRequest request, CancellationToken ct);
    Task DeleteAsync(long id, string? rowVersion, CancellationToken ct);
}

internal sealed class EnrollmentService(EnglishCenterDbContext dbContext) : IEnrollmentService
{
    private static readonly EnrollmentStatus[] OccupyingStatuses = [EnrollmentStatus.PENDING, EnrollmentStatus.ACTIVE];

    public async Task<PagedResponse<EnrollmentResponse>> GetAllAsync(EnrollmentListQuery query, CancellationToken ct)
    {
        var enrollments = BaseQuery(false);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            enrollments = enrollments.Where(item => item.Student.StudentCode.Contains(search)
                || item.Student.FullName.Contains(search) || item.Class.ClassCode.Contains(search)
                || item.Class.ClassName.Contains(search) || item.Class.Course.CourseCode.Contains(search));
        }
        if (query.Status is not null) enrollments = enrollments.Where(item => item.Status == query.Status);
        if (query.StudentId is not null) enrollments = enrollments.Where(item => item.StudentId == query.StudentId);
        if (query.ClassId is not null) enrollments = enrollments.Where(item => item.ClassId == query.ClassId);
        var total = await enrollments.CountAsync(ct);
        var pageItems = await enrollments.OrderByDescending(item => item.EnrolledAtUtc).ThenBy(item => item.EnrollmentId)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
        return new(pageItems.Select(ToResponse).ToArray(), query.Page, query.PageSize, total,
            total == 0 ? 0 : (int)Math.Ceiling(total / (double)query.PageSize));
    }

    public async Task<EnrollmentResponse> GetByIdAsync(long id, CancellationToken ct) => ToResponse(await FindAsync(id, false, ct));

    public async Task<EnrollmentResponse> CreateAsync(CreateEnrollmentRequest request, CancellationToken ct)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var student = await dbContext.Students.SingleOrDefaultAsync(item => item.StudentId == request.StudentId, ct)
            ?? throw new ResourceNotFoundException($"Student {request.StudentId} was not found.");
        if (student.Status != StudentStatus.ACTIVE) throw new ResourceConflictException("Student must be ACTIVE to enroll.");
        var classEntity = await dbContext.Classes.Include(item => item.Course)
            .SingleOrDefaultAsync(item => item.ClassId == request.ClassId, ct)
            ?? throw new ResourceNotFoundException($"Class {request.ClassId} was not found.");
        if (classEntity.Status != ClassStatus.OPEN) throw new ResourceConflictException("Class must be OPEN to accept enrollments.");
        if (await dbContext.Enrollments.IgnoreQueryFilters().AnyAsync(
            item => item.StudentId == student.StudentId && item.ClassId == classEntity.ClassId, ct))
            throw new ResourceConflictException("The student already has an enrollment record for this class.");
        var occupied = await dbContext.Enrollments.CountAsync(
            item => item.ClassId == classEntity.ClassId && OccupyingStatuses.Contains(item.Status), ct);
        if (occupied >= classEntity.Capacity) throw new ResourceConflictException("The class has reached its capacity.");
        var item = new Enrollment
        {
            StudentId = student.StudentId, ClassId = classEntity.ClassId, EnrolledAtUtc = DateTime.UtcNow,
            AgreedTuition = request.AgreedTuition ?? classEntity.Course.StandardTuition,
            Status = EnrollmentStatus.PENDING, Student = student, Class = classEntity
        };
        dbContext.Enrollments.Add(item);
        await dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToResponse(item);
    }

    public async Task<EnrollmentResponse> UpdateAsync(long id, UpdateEnrollmentRequest request, CancellationToken ct)
    {
        var item = await FindAsync(id, true, ct);
        var target = request.Status!.Value;
        ValidateTransition(item.Status, target);
        dbContext.Entry(item).Property(value => value.RowVersion).OriginalValue = RowVersionCodec.Decode(request.RowVersion);
        item.AgreedTuition = request.AgreedTuition!.Value;
        item.Status = target;
        item.CompletionNote = string.IsNullOrWhiteSpace(request.CompletionNote) ? null : request.CompletionNote.Trim();
        await dbContext.SaveChangesAsync(ct);
        return ToResponse(item);
    }

    public async Task DeleteAsync(long id, string? rowVersion, CancellationToken ct)
    {
        var item = await FindAsync(id, true, ct);
        if (item.Status is EnrollmentStatus.ACTIVE or EnrollmentStatus.COMPLETED)
            throw new ResourceConflictException("Active or completed enrollments cannot be deleted.");
        dbContext.Entry(item).Property(value => value.RowVersion).OriginalValue = RowVersionCodec.Decode(rowVersion, "If-Match");
        item.IsDeleted = true;
        await dbContext.SaveChangesAsync(ct);
    }

    private IQueryable<Enrollment> BaseQuery(bool tracking)
    {
        IQueryable<Enrollment> query = tracking ? dbContext.Enrollments : dbContext.Enrollments.AsNoTracking();
        return query.Include(item => item.Student).Include(item => item.Class).ThenInclude(item => item.Course);
    }

    private async Task<Enrollment> FindAsync(long id, bool tracking, CancellationToken ct) =>
        await BaseQuery(tracking).SingleOrDefaultAsync(item => item.EnrollmentId == id, ct)
        ?? throw new ResourceNotFoundException($"Enrollment {id} was not found.");

    private static void ValidateTransition(EnrollmentStatus current, EnrollmentStatus target)
    {
        if (current == target) return;
        var allowed = current switch
        {
            EnrollmentStatus.PENDING => target is EnrollmentStatus.ACTIVE or EnrollmentStatus.CANCELLED,
            EnrollmentStatus.ACTIVE => target is EnrollmentStatus.COMPLETED or EnrollmentStatus.CANCELLED or EnrollmentStatus.WITHDRAWN,
            _ => false
        };
        if (!allowed) throw new ResourceConflictException($"Enrollment status cannot change from {current} to {target}.");
    }

    private static EnrollmentResponse ToResponse(Enrollment item) => new(item.EnrollmentId, item.StudentId,
        item.Student.StudentCode, item.Student.FullName, item.ClassId, item.Class.ClassCode, item.Class.ClassName,
        item.Class.Course.CourseCode, item.EnrolledAtUtc, item.AgreedTuition, item.Status, item.CompletionNote,
        item.CreatedAtUtc, item.UpdatedAtUtc, RowVersionCodec.Encode(item.RowVersion));
}
