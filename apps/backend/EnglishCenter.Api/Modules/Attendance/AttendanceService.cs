using System.Data;
using EnglishCenter.Api.Api.Concurrency;
using EnglishCenter.Api.Api.ErrorHandling;
using EnglishCenter.Api.Api.Paging;
using EnglishCenter.Api.Domain.Entities;
using EnglishCenter.Api.Domain.Enums;
using EnglishCenter.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Modules.Attendance;

public interface IAttendanceService
{
    Task<PagedResponse<AttendanceResponse>> GetAllAsync(AttendanceListQuery query, CancellationToken ct);
    Task<AttendanceResponse> GetByIdAsync(long id, CancellationToken ct);
    Task<AttendanceResponse> CreateAsync(CreateAttendanceRequest request, CancellationToken ct);
    Task<AttendanceResponse> UpdateAsync(long id, UpdateAttendanceRequest request, CancellationToken ct);
    Task DeleteAsync(long id, string? rowVersion, CancellationToken ct);
}

internal sealed class AttendanceService(EnglishCenterDbContext dbContext) : IAttendanceService
{
    public async Task<PagedResponse<AttendanceResponse>> GetAllAsync(AttendanceListQuery query, CancellationToken ct)
    {
        var records = BaseQuery(false);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            records = records.Where(item => item.Enrollment.Student.StudentCode.Contains(search)
                || item.Enrollment.Student.FullName.Contains(search)
                || item.Enrollment.Class.ClassCode.Contains(search)
                || item.Enrollment.Class.ClassName.Contains(search));
        }
        if (query.Status is not null) records = records.Where(item => item.Status == query.Status);
        if (query.EnrollmentId is not null) records = records.Where(item => item.EnrollmentId == query.EnrollmentId);
        if (query.StudentId is not null) records = records.Where(item => item.Enrollment.StudentId == query.StudentId);
        if (query.ClassId is not null) records = records.Where(item => item.Enrollment.ClassId == query.ClassId);
        if (query.DateFrom is not null) records = records.Where(item => item.AttendanceDate >= query.DateFrom);
        if (query.DateTo is not null) records = records.Where(item => item.AttendanceDate <= query.DateTo);

        var total = await records.CountAsync(ct);
        var items = await records.OrderByDescending(item => item.AttendanceDate).ThenBy(item => item.AttendanceId)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
        return new(items.Select(ToResponse).ToArray(), query.Page, query.PageSize, total,
            total == 0 ? 0 : (int)Math.Ceiling(total / (double)query.PageSize));
    }

    public async Task<AttendanceResponse> GetByIdAsync(long id, CancellationToken ct) =>
        ToResponse(await FindAsync(id, false, ct));

    public async Task<AttendanceResponse> CreateAsync(CreateAttendanceRequest request, CancellationToken ct)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var enrollment = await FindEnrollmentAsync(request.EnrollmentId!.Value, ct);
        EnsureCanRecord(enrollment);
        ValidateDate(request.AttendanceDate!.Value, enrollment.Class);
        ValidateCheckIn(request.Status!.Value, request.CheckInAt);
        if (await dbContext.AttendanceRecords.IgnoreQueryFilters().AnyAsync(
            item => item.EnrollmentId == enrollment.EnrollmentId && item.AttendanceDate == request.AttendanceDate, ct))
            throw new ResourceConflictException("Attendance for this enrollment and date already exists.");

        var item = new AttendanceRecord
        {
            EnrollmentId = enrollment.EnrollmentId,
            AttendanceDate = request.AttendanceDate.Value,
            Status = request.Status.Value,
            CheckInAt = request.CheckInAt,
            Note = Normalize(request.Note),
            RecordedBy = Normalize(request.RecordedBy),
            Enrollment = enrollment
        };
        dbContext.AttendanceRecords.Add(item);
        await dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToResponse(item);
    }

    public async Task<AttendanceResponse> UpdateAsync(long id, UpdateAttendanceRequest request, CancellationToken ct)
    {
        var item = await FindAsync(id, true, ct);
        if (item.Enrollment.Class.Status is not (ClassStatus.IN_PROGRESS or ClassStatus.COMPLETED))
            throw new ResourceConflictException("Attendance can be corrected only while the class is in progress or completed.");
        ValidateCheckIn(request.Status!.Value, request.CheckInAt);
        dbContext.Entry(item).Property(value => value.RowVersion).OriginalValue = RowVersionCodec.Decode(request.RowVersion);
        item.Status = request.Status.Value;
        item.CheckInAt = request.CheckInAt;
        item.Note = Normalize(request.Note);
        item.RecordedBy = Normalize(request.RecordedBy);
        await dbContext.SaveChangesAsync(ct);
        return ToResponse(item);
    }

    public async Task DeleteAsync(long id, string? rowVersion, CancellationToken ct)
    {
        var item = await FindAsync(id, true, ct);
        if (item.Enrollment.Class.Status == ClassStatus.COMPLETED)
            throw new ResourceConflictException("Attendance from a completed class cannot be deleted.");
        dbContext.Entry(item).Property(value => value.RowVersion).OriginalValue = RowVersionCodec.Decode(rowVersion, "If-Match");
        item.IsDeleted = true;
        await dbContext.SaveChangesAsync(ct);
    }

    private IQueryable<AttendanceRecord> BaseQuery(bool tracking)
    {
        IQueryable<AttendanceRecord> query = tracking ? dbContext.AttendanceRecords : dbContext.AttendanceRecords.AsNoTracking();
        return query.Include(item => item.Enrollment).ThenInclude(item => item.Student)
            .Include(item => item.Enrollment).ThenInclude(item => item.Class);
    }

    private async Task<AttendanceRecord> FindAsync(long id, bool tracking, CancellationToken ct) =>
        await BaseQuery(tracking).SingleOrDefaultAsync(item => item.AttendanceId == id, ct)
        ?? throw new ResourceNotFoundException($"Attendance {id} was not found.");

    private async Task<Enrollment> FindEnrollmentAsync(long id, CancellationToken ct) =>
        await dbContext.Enrollments.Include(item => item.Student).Include(item => item.Class)
            .SingleOrDefaultAsync(item => item.EnrollmentId == id, ct)
        ?? throw new ResourceNotFoundException($"Enrollment {id} was not found.");

    private static void EnsureCanRecord(Enrollment enrollment)
    {
        if (enrollment.Status != EnrollmentStatus.ACTIVE)
            throw new ResourceConflictException("Enrollment must be ACTIVE before attendance can be recorded.");
        if (enrollment.Class.Status != ClassStatus.IN_PROGRESS)
            throw new ResourceConflictException("Class must be IN_PROGRESS before attendance can be recorded.");
    }

    private static void ValidateDate(DateOnly date, Class classEntity)
    {
        if (date < classEntity.StartDate || date > classEntity.EndDate)
            throw new RequestValidationException("AttendanceDate must be within the class date range.",
                new Dictionary<string, string[]> { ["AttendanceDate"] = ["AttendanceDate must be within the class date range."] });
        if (date > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new RequestValidationException("AttendanceDate cannot be in the future.",
                new Dictionary<string, string[]> { ["AttendanceDate"] = ["AttendanceDate cannot be in the future."] });
    }

    private static void ValidateCheckIn(AttendanceStatus status, DateTime? checkInAt)
    {
        if (status is AttendanceStatus.ABSENT or AttendanceStatus.EXCUSED && checkInAt is not null)
            throw new RequestValidationException("CheckInAt must be empty for ABSENT or EXCUSED attendance.",
                new Dictionary<string, string[]> { ["CheckInAt"] = ["CheckInAt must be empty for ABSENT or EXCUSED attendance."] });
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AttendanceResponse ToResponse(AttendanceRecord item) => new(
        item.AttendanceId, item.EnrollmentId, item.Enrollment.StudentId, item.Enrollment.Student.StudentCode,
        item.Enrollment.Student.FullName, item.Enrollment.ClassId, item.Enrollment.Class.ClassCode,
        item.Enrollment.Class.ClassName, item.AttendanceDate, item.Status, item.CheckInAt, item.Note,
        item.RecordedBy, item.CreatedAtUtc, item.UpdatedAtUtc, RowVersionCodec.Encode(item.RowVersion));
}
