using EnglishCenter.Api.Api.Concurrency;
using EnglishCenter.Api.Api.ErrorHandling;
using EnglishCenter.Api.Api.Paging;
using EnglishCenter.Api.Domain.Entities;
using EnglishCenter.Api.Domain.Enums;
using EnglishCenter.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Modules.Students;

public interface IStudentService
{
    Task<PagedResponse<StudentResponse>> GetAllAsync(StudentListQuery query, CancellationToken cancellationToken);
    Task<StudentResponse> GetByIdAsync(long studentId, CancellationToken cancellationToken);
    Task<StudentResponse> CreateAsync(CreateStudentRequest request, CancellationToken cancellationToken);
    Task<StudentResponse> UpdateAsync(long studentId, UpdateStudentRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(long studentId, string? rowVersion, CancellationToken cancellationToken);
}

internal sealed class StudentService(EnglishCenterDbContext dbContext) : IStudentService
{
    public async Task<PagedResponse<StudentResponse>> GetAllAsync(
        StudentListQuery query,
        CancellationToken cancellationToken)
    {
        var students = dbContext.Students.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            students = students.Where(student =>
                student.StudentCode.Contains(search)
                || student.FullName.Contains(search)
                || (student.Email != null && student.Email.Contains(search))
                || (student.Phone != null && student.Phone.Contains(search)));
        }

        if (query.Status is not null)
        {
            students = students.Where(student => student.Status == query.Status);
        }

        var totalCount = await students.CountAsync(cancellationToken);
        var pageItems = await students
            .OrderBy(student => student.FullName)
            .ThenBy(student => student.StudentId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);
        var items = pageItems.Select(ToResponse).ToArray();

        return new PagedResponse<StudentResponse>(
            items,
            query.Page,
            query.PageSize,
            totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)query.PageSize));
    }

    public async Task<StudentResponse> GetByIdAsync(
        long studentId,
        CancellationToken cancellationToken)
    {
        var student = await FindAsync(studentId, asTracking: false, cancellationToken);
        return ToResponse(student);
    }

    public async Task<StudentResponse> CreateAsync(
        CreateStudentRequest request,
        CancellationToken cancellationToken)
    {
        ValidateDateOfBirth(request.DateOfBirth);

        var studentCode = NormalizeCode(request.StudentCode!);
        await EnsureCodeAvailableAsync(studentCode, null, cancellationToken);

        var student = new Student
        {
            StudentCode = studentCode,
            FullName = request.FullName!.Trim(),
            DateOfBirth = request.DateOfBirth,
            Gender = NormalizeOptional(request.Gender),
            Email = NormalizeOptional(request.Email),
            Phone = NormalizeOptional(request.Phone),
            GuardianName = NormalizeOptional(request.GuardianName),
            GuardianPhone = NormalizeOptional(request.GuardianPhone),
            Status = StudentStatus.ACTIVE
        };

        dbContext.Students.Add(student);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(student);
    }

    public async Task<StudentResponse> UpdateAsync(
        long studentId,
        UpdateStudentRequest request,
        CancellationToken cancellationToken)
    {
        ValidateDateOfBirth(request.DateOfBirth);

        var student = await FindAsync(studentId, asTracking: true, cancellationToken);
        var studentCode = NormalizeCode(request.StudentCode!);
        await EnsureCodeAvailableAsync(studentCode, studentId, cancellationToken);

        dbContext.Entry(student)
            .Property(item => item.RowVersion)
            .OriginalValue = RowVersionCodec.Decode(request.RowVersion);

        student.StudentCode = studentCode;
        student.FullName = request.FullName!.Trim();
        student.DateOfBirth = request.DateOfBirth;
        student.Gender = NormalizeOptional(request.Gender);
        student.Email = NormalizeOptional(request.Email);
        student.Phone = NormalizeOptional(request.Phone);
        student.GuardianName = NormalizeOptional(request.GuardianName);
        student.GuardianPhone = NormalizeOptional(request.GuardianPhone);
        student.Status = request.Status!.Value;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(student);
    }

    public async Task DeleteAsync(
        long studentId,
        string? rowVersion,
        CancellationToken cancellationToken)
    {
        var student = await FindAsync(studentId, asTracking: true, cancellationToken);

        dbContext.Entry(student)
            .Property(item => item.RowVersion)
            .OriginalValue = RowVersionCodec.Decode(rowVersion, "If-Match");

        student.IsDeleted = true;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Student> FindAsync(
        long studentId,
        bool asTracking,
        CancellationToken cancellationToken)
    {
        var query = asTracking
            ? dbContext.Students
            : dbContext.Students.AsNoTracking();

        return await query.SingleOrDefaultAsync(
                item => item.StudentId == studentId,
                cancellationToken)
            ?? throw new ResourceNotFoundException($"Student {studentId} was not found.");
    }

    private async Task EnsureCodeAvailableAsync(
        string studentCode,
        long? currentStudentId,
        CancellationToken cancellationToken)
    {
        var codeExists = await dbContext.Students
            .IgnoreQueryFilters()
            .AnyAsync(
                item => item.StudentCode == studentCode
                    && (!currentStudentId.HasValue || item.StudentId != currentStudentId.Value),
                cancellationToken);

        if (codeExists)
        {
            throw new ResourceConflictException($"StudentCode {studentCode} already exists.");
        }
    }

    private static void ValidateDateOfBirth(DateOnly? dateOfBirth)
    {
        if (dateOfBirth is not null && dateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new RequestValidationException(
                "DateOfBirth cannot be in the future.",
                new Dictionary<string, string[]>
                {
                    [nameof(dateOfBirth)] = ["DateOfBirth cannot be in the future."]
                });
        }
    }

    private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static StudentResponse ToResponse(Student student)
    {
        return new StudentResponse(
            student.StudentId,
            student.StudentCode,
            student.FullName,
            student.DateOfBirth,
            student.Gender,
            student.Email,
            student.Phone,
            student.GuardianName,
            student.GuardianPhone,
            student.Status,
            student.CreatedAtUtc,
            student.UpdatedAtUtc,
            RowVersionCodec.Encode(student.RowVersion));
    }
}
