using EnglishCenter.Api.Api.ErrorHandling;
using EnglishCenter.Api.Domain.Entities;
using EnglishCenter.Api.Domain.Enums;
using EnglishCenter.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Modules.Students;

public interface IStudentService
{
    Task<StudentResponse> GetByIdAsync(long studentId, CancellationToken cancellationToken);
    Task<StudentResponse> CreateAsync(CreateStudentRequest request, CancellationToken cancellationToken);
}

internal sealed class StudentService(EnglishCenterDbContext dbContext) : IStudentService
{
    public async Task<StudentResponse> GetByIdAsync(
        long studentId,
        CancellationToken cancellationToken)
    {
        var student = await dbContext.Students
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.StudentId == studentId, cancellationToken)
            ?? throw new ResourceNotFoundException($"Student {studentId} was not found.");

        return ToResponse(student);
    }

    public async Task<StudentResponse> CreateAsync(
        CreateStudentRequest request,
        CancellationToken cancellationToken)
    {
        var studentCode = request.StudentCode!.Trim().ToUpperInvariant();
        var codeExists = await dbContext.Students
            .IgnoreQueryFilters()
            .AnyAsync(item => item.StudentCode == studentCode, cancellationToken);

        if (codeExists)
        {
            throw new ResourceConflictException($"StudentCode {studentCode} already exists.");
        }

        if (request.DateOfBirth is not null && request.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new RequestValidationException(
                "DateOfBirth cannot be in the future.",
                new Dictionary<string, string[]>
                {
                    [nameof(request.DateOfBirth)] = ["DateOfBirth cannot be in the future."]
                });
        }

        var student = new Student
        {
            StudentCode = studentCode,
            FullName = request.FullName!.Trim(),
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            Email = request.Email?.Trim(),
            Phone = request.Phone?.Trim(),
            GuardianName = request.GuardianName?.Trim(),
            GuardianPhone = request.GuardianPhone?.Trim(),
            Status = StudentStatus.ACTIVE
        };

        dbContext.Students.Add(student);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(student);
    }

    private static StudentResponse ToResponse(Student student)
    {
        return new StudentResponse(
            StudentId: student.StudentId,
            StudentCode: student.StudentCode,
            FullName: student.FullName,
            DateOfBirth: student.DateOfBirth,
            Gender: student.Gender,
            Email: student.Email,
            Phone: student.Phone,
            Status: student.Status,
            RowVersion: student.RowVersion);
    }
}
