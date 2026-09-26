using EnglishCenter.Api.Api.Concurrency;
using EnglishCenter.Api.Api.ErrorHandling;
using EnglishCenter.Api.Api.Paging;
using EnglishCenter.Api.Domain.Entities;
using EnglishCenter.Api.Domain.Enums;
using EnglishCenter.Api.Infrastructure.Persistence;
using EnglishCenter.Api.Modules.Caching;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Modules.Courses;

public interface ICourseService
{
    Task<PagedResponse<CourseResponse>> GetAllAsync(CourseListQuery query, CancellationToken cancellationToken);
    Task<CourseResponse> GetByIdAsync(long courseId, CancellationToken cancellationToken);
    Task<CourseResponse> CreateAsync(CreateCourseRequest request, CancellationToken cancellationToken);
    Task<CourseResponse> UpdateAsync(long courseId, UpdateCourseRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(long courseId, string? rowVersion, CancellationToken cancellationToken);
}

internal sealed class CourseService(
    EnglishCenterDbContext dbContext,
    ICourseCache courseCache) : ICourseService
{
    public async Task<PagedResponse<CourseResponse>> GetAllAsync(CourseListQuery query, CancellationToken cancellationToken)
    {
        var cached = await courseCache.GetListAsync(query, cancellationToken);
        if (cached.Found && cached.Value is not null) return cached.Value;

        var courses = dbContext.Courses.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            courses = courses.Where(course =>
                course.CourseCode.Contains(search)
                || course.CourseName.Contains(search)
                || course.LevelCode.Contains(search));
        }

        if (query.Status is not null)
        {
            courses = courses.Where(course => course.Status == query.Status);
        }

        var totalCount = await courses.CountAsync(cancellationToken);
        var pageItems = await courses.OrderBy(course => course.CourseName)
            .ThenBy(course => course.CourseId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);

        var response = new PagedResponse<CourseResponse>(
            pageItems.Select(ToResponse).ToArray(), query.Page, query.PageSize, totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)query.PageSize));
        await courseCache.SetListAsync(query, response, cancellationToken);
        return response;
    }

    public async Task<CourseResponse> GetByIdAsync(long courseId, CancellationToken cancellationToken)
    {
        var cached = await courseCache.GetByIdAsync(courseId, cancellationToken);
        if (cached.Found && cached.Value is not null) return cached.Value;

        var response = ToResponse(await FindAsync(courseId, false, cancellationToken));
        await courseCache.SetByIdAsync(response, cancellationToken);
        return response;
    }

    public async Task<CourseResponse> CreateAsync(CreateCourseRequest request, CancellationToken cancellationToken)
    {
        var courseCode = NormalizeCode(request.CourseCode!);
        await EnsureCodeAvailableAsync(courseCode, null, cancellationToken);
        var course = new Course
        {
            CourseCode = courseCode,
            CourseName = request.CourseName!.Trim(),
            LevelCode = NormalizeCode(request.LevelCode!),
            Description = NormalizeOptional(request.Description),
            PlannedHours = request.PlannedHours!.Value,
            StandardTuition = request.StandardTuition!.Value,
            Status = CourseStatus.DRAFT
        };
        dbContext.Courses.Add(course);
        await dbContext.SaveChangesAsync(cancellationToken);
        await courseCache.InvalidateListsAsync(cancellationToken);
        return ToResponse(course);
    }

    public async Task<CourseResponse> UpdateAsync(long courseId, UpdateCourseRequest request, CancellationToken cancellationToken)
    {
        var course = await FindAsync(courseId, true, cancellationToken);
        var courseCode = NormalizeCode(request.CourseCode!);
        await EnsureCodeAvailableAsync(courseCode, courseId, cancellationToken);
        ValidateTransition(course.Status, request.Status!.Value);
        if (request.Status.Value == CourseStatus.ARCHIVED
            && await dbContext.Classes.AnyAsync(item => item.CourseId == courseId
                && (item.Status == ClassStatus.PLANNED
                    || item.Status == ClassStatus.OPEN
                    || item.Status == ClassStatus.IN_PROGRESS), cancellationToken))
        {
            throw new ResourceConflictException("A course with unfinished classes cannot be archived.");
        }
        dbContext.Entry(course).Property(item => item.RowVersion).OriginalValue = RowVersionCodec.Decode(request.RowVersion);
        course.CourseCode = courseCode;
        course.CourseName = request.CourseName!.Trim();
        course.LevelCode = NormalizeCode(request.LevelCode!);
        course.Description = NormalizeOptional(request.Description);
        course.PlannedHours = request.PlannedHours!.Value;
        course.StandardTuition = request.StandardTuition!.Value;
        course.Status = request.Status!.Value;
        await dbContext.SaveChangesAsync(cancellationToken);
        await courseCache.InvalidateAsync(courseId, cancellationToken);
        return ToResponse(course);
    }

    public async Task DeleteAsync(long courseId, string? rowVersion, CancellationToken cancellationToken)
    {
        var course = await FindAsync(courseId, true, cancellationToken);
        if (await dbContext.Classes.AnyAsync(item => item.CourseId == courseId, cancellationToken))
        {
            throw new ResourceConflictException("The course still has active classes and cannot be deleted.");
        }

        dbContext.Entry(course).Property(item => item.RowVersion).OriginalValue = RowVersionCodec.Decode(rowVersion, "If-Match");
        course.IsDeleted = true;
        await dbContext.SaveChangesAsync(cancellationToken);
        await courseCache.InvalidateAsync(courseId, cancellationToken);
    }

    private async Task<Course> FindAsync(long id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? dbContext.Courses : dbContext.Courses.AsNoTracking();
        return await query.SingleOrDefaultAsync(item => item.CourseId == id, cancellationToken)
            ?? throw new ResourceNotFoundException($"Course {id} was not found.");
    }

    private async Task EnsureCodeAvailableAsync(string code, long? currentId, CancellationToken cancellationToken)
    {
        if (await dbContext.Courses.IgnoreQueryFilters().AnyAsync(
            item => item.CourseCode == code && (!currentId.HasValue || item.CourseId != currentId.Value), cancellationToken))
        {
            throw new ResourceConflictException($"CourseCode {code} already exists.");
        }
    }

    private static void ValidateTransition(CourseStatus current, CourseStatus target)
    {
        if (current == target) return;
        var allowed = current switch
        {
            CourseStatus.DRAFT => target is CourseStatus.ACTIVE or CourseStatus.INACTIVE,
            CourseStatus.ACTIVE => target is CourseStatus.INACTIVE or CourseStatus.ARCHIVED,
            CourseStatus.INACTIVE => target is CourseStatus.ACTIVE or CourseStatus.ARCHIVED,
            _ => false
        };
        if (!allowed) throw new ResourceConflictException($"Course status cannot change from {current} to {target}.");
    }
    private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static CourseResponse ToResponse(Course course) => new(
        course.CourseId, course.CourseCode, course.CourseName, course.LevelCode, course.Description,
        course.PlannedHours, course.StandardTuition, course.Status, course.CreatedAtUtc, course.UpdatedAtUtc,
        RowVersionCodec.Encode(course.RowVersion));
}
