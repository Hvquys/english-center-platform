using EnglishCenter.Api.Domain.Entities;
using EnglishCenter.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Infrastructure.Persistence;

internal static class SeedData
{
    private static readonly DateTime SeedTimestamp =
        new(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc);

    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Student>().HasData(new
        {
            StudentId = 1001L,
            StudentCode = "STU-DEMO-001",
            FullName = "Nguyen Minh Anh",
            DateOfBirth = new DateOnly(2010, 5, 12),
            Gender = "FEMALE",
            Email = "student.demo@example.test",
            Phone = "0900000001",
            GuardianName = "Nguyen Van Minh",
            GuardianPhone = "0900000011",
            Status = StudentStatus.ACTIVE,
            CreatedAtUtc = SeedTimestamp,
            UpdatedAtUtc = SeedTimestamp,
            IsDeleted = false
        });

        modelBuilder.Entity<Teacher>().HasData(new
        {
            TeacherId = 1001L,
            TeacherCode = "TCH-DEMO-001",
            FullName = "Tran Thu Ha",
            Email = "teacher.demo@example.test",
            Phone = "0900000002",
            Specialization = "General English",
            Status = TeacherStatus.ACTIVE,
            CreatedAtUtc = SeedTimestamp,
            UpdatedAtUtc = SeedTimestamp,
            IsDeleted = false
        });

        modelBuilder.Entity<Course>().HasData(new
        {
            CourseId = 1001L,
            CourseCode = "ENG-A1-DEMO",
            CourseName = "English A1 Foundation",
            LevelCode = "A1",
            Description = "Deterministic local development seed course.",
            PlannedHours = (short)40,
            StandardTuition = 3000000m,
            Status = CourseStatus.ACTIVE,
            CreatedAtUtc = SeedTimestamp,
            UpdatedAtUtc = SeedTimestamp,
            IsDeleted = false
        });

        modelBuilder.Entity<Class>().HasData(new
        {
            ClassId = 1001L,
            ClassCode = "CLS-A1-DEMO-001",
            CourseId = 1001L,
            TeacherId = (long?)1001L,
            ClassName = "A1 Demo Evening Class",
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 12, 31),
            Capacity = (short)20,
            ScheduleNote = "Tuesday and Thursday, 18:30-20:00",
            RoomName = "Room A1",
            Status = ClassStatus.OPEN,
            CreatedAtUtc = SeedTimestamp,
            UpdatedAtUtc = SeedTimestamp,
            IsDeleted = false
        });

        modelBuilder.Entity<Enrollment>().HasData(new
        {
            EnrollmentId = 1001L,
            StudentId = 1001L,
            ClassId = 1001L,
            EnrolledAtUtc = SeedTimestamp,
            AgreedTuition = 2900000m,
            Status = EnrollmentStatus.ACTIVE,
            CompletionNote = (string?)null,
            CreatedAtUtc = SeedTimestamp,
            UpdatedAtUtc = SeedTimestamp,
            IsDeleted = false
        });

        modelBuilder.Entity<AttendanceRecord>().HasData(new
        {
            AttendanceId = 1001L,
            EnrollmentId = 1001L,
            AttendanceDate = new DateOnly(2026, 10, 1),
            Status = AttendanceStatus.PRESENT,
            CheckInAt = new DateTime(2026, 10, 1, 18, 25, 0, DateTimeKind.Utc),
            Note = (string?)null,
            RecordedBy = "seed",
            CreatedAtUtc = SeedTimestamp,
            UpdatedAtUtc = SeedTimestamp,
            IsDeleted = false
        });

        modelBuilder.Entity<Payment>().HasData(new
        {
            PaymentId = 1001L,
            EnrollmentId = 1001L,
            PaymentDate = new DateOnly(2026, 9, 25),
            Amount = 1450000m,
            PaymentMethod = PaymentMethod.BANK_TRANSFER,
            PaymentReference = "DEMO-PAYMENT-001",
            Status = PaymentStatus.COMPLETED,
            Note = "Deterministic development seed payment.",
            CreatedAtUtc = SeedTimestamp,
            UpdatedAtUtc = SeedTimestamp,
            IsDeleted = false
        });
    }
}
