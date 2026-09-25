using EnglishCenter.Api.Api.Concurrency;
using EnglishCenter.Api.Api.ErrorHandling;
using EnglishCenter.Api.Api.Paging;
using EnglishCenter.Api.Domain.Entities;
using EnglishCenter.Api.Domain.Enums;
using EnglishCenter.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Modules.Classes;

public interface IClassService
{
    Task<PagedResponse<ClassResponse>> GetAllAsync(ClassListQuery query, CancellationToken ct);
    Task<ClassResponse> GetByIdAsync(long id, CancellationToken ct);
    Task<ClassResponse> CreateAsync(CreateClassRequest request, CancellationToken ct);
    Task<ClassResponse> UpdateAsync(long id, UpdateClassRequest request, CancellationToken ct);
    Task DeleteAsync(long id, string? rowVersion, CancellationToken ct);
}

internal sealed class ClassService(EnglishCenterDbContext dbContext) : IClassService
{
    private static readonly EnrollmentStatus[] OccupyingStatuses = [EnrollmentStatus.PENDING, EnrollmentStatus.ACTIVE];

    public async Task<PagedResponse<ClassResponse>> GetAllAsync(ClassListQuery query, CancellationToken ct)
    {
        IQueryable<Class> classes = dbContext.Classes.AsNoTracking().Include(item => item.Course).Include(item => item.Teacher);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            classes = classes.Where(item => item.ClassCode.Contains(search) || item.ClassName.Contains(search)
                || item.Course.CourseCode.Contains(search) || (item.Teacher != null && item.Teacher.FullName.Contains(search)));
        }
        if (query.Status is not null) classes = classes.Where(item => item.Status == query.Status);
        if (query.CourseId is not null) classes = classes.Where(item => item.CourseId == query.CourseId);
        if (query.TeacherId is not null) classes = classes.Where(item => item.TeacherId == query.TeacherId);
        var total = await classes.CountAsync(ct);
        var pageItems = await classes.OrderBy(item => item.StartDate).ThenBy(item => item.ClassName)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
        return new(pageItems.Select(ToResponse).ToArray(), query.Page, query.PageSize, total,
            total == 0 ? 0 : (int)Math.Ceiling(total / (double)query.PageSize));
    }

    public async Task<ClassResponse> GetByIdAsync(long id, CancellationToken ct) => ToResponse(await FindAsync(id, false, ct));

    public async Task<ClassResponse> CreateAsync(CreateClassRequest request, CancellationToken ct)
    {
        ValidateDates(request.StartDate!.Value, request.EndDate!.Value);
        var code = NormalizeCode(request.ClassCode!);
        await EnsureCodeAvailableAsync(code, null, ct);
        var course = await GetCourseAsync(request.CourseId!.Value, ct);
        var teacher = await GetTeacherAsync(request.TeacherId, ct);
        ValidateReferences(course, teacher, ClassStatus.PLANNED);
        var item = new Class
        {
            ClassCode = code,
            CourseId = course.CourseId,
            TeacherId = teacher?.TeacherId,
            ClassName = request.ClassName!.Trim(),
            StartDate = request.StartDate.Value,
            EndDate = request.EndDate.Value,
            Capacity = request.Capacity!.Value,
            ScheduleNote = NormalizeOptional(request.ScheduleNote),
            RoomName = NormalizeOptional(request.RoomName),
            Status = ClassStatus.PLANNED,
            Course = course,
            Teacher = teacher
        };
        dbContext.Classes.Add(item);
        await dbContext.SaveChangesAsync(ct);
        return ToResponse(item);
    }

    public async Task<ClassResponse> UpdateAsync(long id, UpdateClassRequest request, CancellationToken ct)
    {
        ValidateDates(request.StartDate!.Value, request.EndDate!.Value);
        var item = await FindAsync(id, true, ct);
        var targetStatus = request.Status!.Value;
        ValidateTransition(item.Status, targetStatus);
        var code = NormalizeCode(request.ClassCode!);
        await EnsureCodeAvailableAsync(code, id, ct);
        var course = await GetCourseAsync(request.CourseId!.Value, ct);
        var teacher = await GetTeacherAsync(request.TeacherId, ct);
        ValidateReferences(course, teacher, targetStatus);
        var enrollmentCount = await dbContext.Enrollments.CountAsync(
            enrollment => enrollment.ClassId == id && OccupyingStatuses.Contains(enrollment.Status), ct);
        if (request.Capacity!.Value < enrollmentCount)
            throw new ResourceConflictException($"Capacity cannot be lower than {enrollmentCount} occupied seats.");
        if (item.CourseId != request.CourseId.Value && await dbContext.Enrollments.AnyAsync(e => e.ClassId == id, ct))
            throw new ResourceConflictException("Course cannot be changed after the class has enrollments.");
        dbContext.Entry(item).Property(value => value.RowVersion).OriginalValue = RowVersionCodec.Decode(request.RowVersion);
        item.ClassCode = code; item.CourseId = course.CourseId; item.TeacherId = teacher?.TeacherId;
        item.ClassName = request.ClassName!.Trim(); item.StartDate = request.StartDate.Value; item.EndDate = request.EndDate.Value;
        item.Capacity = request.Capacity.Value; item.ScheduleNote = NormalizeOptional(request.ScheduleNote);
        item.RoomName = NormalizeOptional(request.RoomName); item.Status = targetStatus; item.Course = course; item.Teacher = teacher;
        await dbContext.SaveChangesAsync(ct);
        return ToResponse(item);
    }

    public async Task DeleteAsync(long id, string? rowVersion, CancellationToken ct)
    {
        var item = await FindAsync(id, true, ct);
        if (await dbContext.Enrollments.AnyAsync(e => e.ClassId == id && OccupyingStatuses.Contains(e.Status), ct))
            throw new ResourceConflictException("The class has pending or active enrollments and cannot be deleted.");
        dbContext.Entry(item).Property(value => value.RowVersion).OriginalValue = RowVersionCodec.Decode(rowVersion, "If-Match");
        item.IsDeleted = true;
        await dbContext.SaveChangesAsync(ct);
    }

    private async Task<Class> FindAsync(long id, bool tracking, CancellationToken ct)
    {
        IQueryable<Class> query = tracking ? dbContext.Classes : dbContext.Classes.AsNoTracking();
        return await query.Include(item => item.Course).Include(item => item.Teacher)
            .SingleOrDefaultAsync(item => item.ClassId == id, ct)
            ?? throw new ResourceNotFoundException($"Class {id} was not found.");
    }

    private async Task<Course> GetCourseAsync(long id, CancellationToken ct) =>
        await dbContext.Courses.SingleOrDefaultAsync(item => item.CourseId == id, ct)
        ?? throw new ResourceNotFoundException($"Course {id} was not found.");

    private async Task<Teacher?> GetTeacherAsync(long? id, CancellationToken ct) => id is null ? null :
        await dbContext.Teachers.SingleOrDefaultAsync(item => item.TeacherId == id, ct)
        ?? throw new ResourceNotFoundException($"Teacher {id} was not found.");

    private static void ValidateReferences(Course course, Teacher? teacher, ClassStatus status)
    {
        if ((status is ClassStatus.PLANNED && course.Status == CourseStatus.ARCHIVED)
            || (status is ClassStatus.OPEN or ClassStatus.IN_PROGRESS && course.Status != CourseStatus.ACTIVE))
        {
            throw new ResourceConflictException("The selected course is not available for this class status.");
        }

        if (teacher is not null
            && status is ClassStatus.PLANNED or ClassStatus.OPEN or ClassStatus.IN_PROGRESS
            && teacher.Status != TeacherStatus.ACTIVE)
        {
            throw new ResourceConflictException("The selected teacher must be ACTIVE.");
        }
    }

    private static void ValidateDates(DateOnly start, DateOnly end)
    {
        if (end < start) throw new RequestValidationException("EndDate cannot be before StartDate.",
            new Dictionary<string, string[]> { ["EndDate"] = ["EndDate cannot be before StartDate."] });
    }

    private static void ValidateTransition(ClassStatus current, ClassStatus target)
    {
        if (current == target) return;
        var allowed = current switch
        {
            ClassStatus.PLANNED => target is ClassStatus.OPEN or ClassStatus.CANCELLED,
            ClassStatus.OPEN => target is ClassStatus.IN_PROGRESS or ClassStatus.CANCELLED,
            ClassStatus.IN_PROGRESS => target is ClassStatus.COMPLETED or ClassStatus.CANCELLED,
            _ => false
        };
        if (!allowed) throw new ResourceConflictException($"Class status cannot change from {current} to {target}.");
    }

    private async Task EnsureCodeAvailableAsync(string code, long? id, CancellationToken ct)
    {
        if (await dbContext.Classes.IgnoreQueryFilters().AnyAsync(item => item.ClassCode == code && (!id.HasValue || item.ClassId != id), ct))
            throw new ResourceConflictException($"ClassCode {code} already exists.");
    }

    private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static ClassResponse ToResponse(Class item) => new(item.ClassId, item.ClassCode, item.CourseId,
        item.Course.CourseCode, item.Course.CourseName, item.TeacherId, item.Teacher?.TeacherCode, item.Teacher?.FullName,
        item.ClassName, item.StartDate, item.EndDate, item.Capacity, item.ScheduleNote, item.RoomName, item.Status,
        item.CreatedAtUtc, item.UpdatedAtUtc, RowVersionCodec.Encode(item.RowVersion));
}
