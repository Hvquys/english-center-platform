using EnglishCenter.Api.Api.Concurrency;
using EnglishCenter.Api.Api.ErrorHandling;
using EnglishCenter.Api.Api.Paging;
using EnglishCenter.Api.Domain.Entities;
using EnglishCenter.Api.Domain.Enums;
using EnglishCenter.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Modules.Teachers;

public interface ITeacherService
{
    Task<PagedResponse<TeacherResponse>> GetAllAsync(TeacherListQuery query, CancellationToken cancellationToken);
    Task<TeacherResponse> GetByIdAsync(long teacherId, CancellationToken cancellationToken);
    Task<TeacherResponse> CreateAsync(CreateTeacherRequest request, CancellationToken cancellationToken);
    Task<TeacherResponse> UpdateAsync(long teacherId, UpdateTeacherRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(long teacherId, string? rowVersion, CancellationToken cancellationToken);
}

internal sealed class TeacherService(EnglishCenterDbContext dbContext) : ITeacherService
{
    public async Task<PagedResponse<TeacherResponse>> GetAllAsync(
        TeacherListQuery query,
        CancellationToken cancellationToken)
    {
        var teachers = dbContext.Teachers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            teachers = teachers.Where(teacher =>
                teacher.TeacherCode.Contains(search)
                || teacher.FullName.Contains(search)
                || (teacher.Email != null && teacher.Email.Contains(search))
                || (teacher.Phone != null && teacher.Phone.Contains(search))
                || (teacher.Specialization != null && teacher.Specialization.Contains(search)));
        }

        if (query.Status is not null)
        {
            teachers = teachers.Where(teacher => teacher.Status == query.Status);
        }

        var totalCount = await teachers.CountAsync(cancellationToken);
        var pageItems = await teachers
            .OrderBy(teacher => teacher.FullName)
            .ThenBy(teacher => teacher.TeacherId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);
        var items = pageItems.Select(ToResponse).ToArray();

        return new PagedResponse<TeacherResponse>(
            items,
            query.Page,
            query.PageSize,
            totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)query.PageSize));
    }

    public async Task<TeacherResponse> GetByIdAsync(
        long teacherId,
        CancellationToken cancellationToken)
    {
        var teacher = await FindAsync(teacherId, asTracking: false, cancellationToken);
        return ToResponse(teacher);
    }

    public async Task<TeacherResponse> CreateAsync(
        CreateTeacherRequest request,
        CancellationToken cancellationToken)
    {
        var teacherCode = NormalizeCode(request.TeacherCode!);
        await EnsureCodeAvailableAsync(teacherCode, null, cancellationToken);

        var teacher = new Teacher
        {
            TeacherCode = teacherCode,
            FullName = request.FullName!.Trim(),
            Email = NormalizeOptional(request.Email),
            Phone = NormalizeOptional(request.Phone),
            Specialization = NormalizeOptional(request.Specialization),
            Status = TeacherStatus.ACTIVE
        };

        dbContext.Teachers.Add(teacher);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(teacher);
    }

    public async Task<TeacherResponse> UpdateAsync(
        long teacherId,
        UpdateTeacherRequest request,
        CancellationToken cancellationToken)
    {
        var teacher = await FindAsync(teacherId, asTracking: true, cancellationToken);
        var teacherCode = NormalizeCode(request.TeacherCode!);
        await EnsureCodeAvailableAsync(teacherCode, teacherId, cancellationToken);

        dbContext.Entry(teacher)
            .Property(item => item.RowVersion)
            .OriginalValue = RowVersionCodec.Decode(request.RowVersion);

        teacher.TeacherCode = teacherCode;
        teacher.FullName = request.FullName!.Trim();
        teacher.Email = NormalizeOptional(request.Email);
        teacher.Phone = NormalizeOptional(request.Phone);
        teacher.Specialization = NormalizeOptional(request.Specialization);
        teacher.Status = request.Status!.Value;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(teacher);
    }

    public async Task DeleteAsync(
        long teacherId,
        string? rowVersion,
        CancellationToken cancellationToken)
    {
        var teacher = await FindAsync(teacherId, asTracking: true, cancellationToken);

        dbContext.Entry(teacher)
            .Property(item => item.RowVersion)
            .OriginalValue = RowVersionCodec.Decode(rowVersion, "If-Match");

        teacher.IsDeleted = true;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Teacher> FindAsync(
        long teacherId,
        bool asTracking,
        CancellationToken cancellationToken)
    {
        var query = asTracking
            ? dbContext.Teachers
            : dbContext.Teachers.AsNoTracking();

        return await query.SingleOrDefaultAsync(
                item => item.TeacherId == teacherId,
                cancellationToken)
            ?? throw new ResourceNotFoundException($"Teacher {teacherId} was not found.");
    }

    private async Task EnsureCodeAvailableAsync(
        string teacherCode,
        long? currentTeacherId,
        CancellationToken cancellationToken)
    {
        var codeExists = await dbContext.Teachers
            .IgnoreQueryFilters()
            .AnyAsync(
                item => item.TeacherCode == teacherCode
                    && (!currentTeacherId.HasValue || item.TeacherId != currentTeacherId.Value),
                cancellationToken);

        if (codeExists)
        {
            throw new ResourceConflictException($"TeacherCode {teacherCode} already exists.");
        }
    }

    private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static TeacherResponse ToResponse(Teacher teacher)
    {
        return new TeacherResponse(
            teacher.TeacherId,
            teacher.TeacherCode,
            teacher.FullName,
            teacher.Email,
            teacher.Phone,
            teacher.Specialization,
            teacher.Status,
            teacher.CreatedAtUtc,
            teacher.UpdatedAtUtc,
            RowVersionCodec.Encode(teacher.RowVersion));
    }
}
