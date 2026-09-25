using EnglishCenter.Api.Domain.Entities;
using EnglishCenter.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnglishCenter.Api.Infrastructure.Persistence.Configurations;

internal static class AuditableEntityConfiguration
{
    public static void Configure<TEntity>(EntityTypeBuilder<TEntity> builder)
        where TEntity : AuditableEntity
    {
        builder.Property(entity => entity.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(entity => entity.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(entity => entity.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(entity => entity.RowVersion)
            .HasColumnName("row_version")
            .IsRowVersion();

        builder.HasQueryFilter(entity => !entity.IsDeleted);
    }
}

internal sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students", table =>
        {
            table.HasTrigger("TR_Students_SetUpdatedAtUtc");
            table.HasCheckConstraint(
                "CK_Students_Gender",
                "[gender] IS NULL OR [gender] IN ('MALE', 'FEMALE', 'OTHER')");
            table.HasCheckConstraint(
                "CK_Students_Status",
                "[status] IN ('ACTIVE', 'INACTIVE', 'GRADUATED', 'SUSPENDED')");
        });

        builder.HasKey(entity => entity.StudentId).HasName("PK_Students");
        builder.Property(entity => entity.StudentId).HasColumnName("student_id");
        builder.Property(entity => entity.StudentCode).HasColumnName("student_code").HasMaxLength(20);
        builder.Property(entity => entity.FullName).HasColumnName("full_name").HasMaxLength(150);
        builder.Property(entity => entity.DateOfBirth).HasColumnName("date_of_birth").HasColumnType("date");
        builder.Property(entity => entity.Gender).HasColumnName("gender").HasMaxLength(10).IsUnicode(false);
        builder.Property(entity => entity.Email).HasColumnName("email").HasMaxLength(320);
        builder.Property(entity => entity.Phone).HasColumnName("phone").HasMaxLength(30);
        builder.Property(entity => entity.GuardianName).HasColumnName("guardian_name").HasMaxLength(150);
        builder.Property(entity => entity.GuardianPhone).HasColumnName("guardian_phone").HasMaxLength(30);
        builder.Property(entity => entity.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValueSql("'ACTIVE'");

        builder.HasIndex(entity => entity.StudentCode)
            .IsUnique()
            .HasDatabaseName("UQ_Students_StudentCode");
        builder.HasIndex(entity => new { entity.UpdatedAtUtc, entity.StudentId })
            .HasDatabaseName("IX_Students_UpdatedAtUtc");

        AuditableEntityConfiguration.Configure(builder);
    }
}

internal sealed class TeacherConfiguration : IEntityTypeConfiguration<Teacher>
{
    public void Configure(EntityTypeBuilder<Teacher> builder)
    {
        builder.ToTable("Teachers", table =>
        {
            table.HasTrigger("TR_Teachers_SetUpdatedAtUtc");
            table.HasCheckConstraint(
                "CK_Teachers_Status",
                "[status] IN ('ACTIVE', 'INACTIVE', 'ON_LEAVE')");
        });

        builder.HasKey(entity => entity.TeacherId).HasName("PK_Teachers");
        builder.Property(entity => entity.TeacherId).HasColumnName("teacher_id");
        builder.Property(entity => entity.TeacherCode).HasColumnName("teacher_code").HasMaxLength(20);
        builder.Property(entity => entity.FullName).HasColumnName("full_name").HasMaxLength(150);
        builder.Property(entity => entity.Email).HasColumnName("email").HasMaxLength(320);
        builder.Property(entity => entity.Phone).HasColumnName("phone").HasMaxLength(30);
        builder.Property(entity => entity.Specialization).HasColumnName("specialization").HasMaxLength(200);
        builder.Property(entity => entity.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValueSql("'ACTIVE'");

        builder.HasIndex(entity => entity.TeacherCode)
            .IsUnique()
            .HasDatabaseName("UQ_Teachers_TeacherCode");
        builder.HasIndex(entity => new { entity.UpdatedAtUtc, entity.TeacherId })
            .HasDatabaseName("IX_Teachers_UpdatedAtUtc");

        AuditableEntityConfiguration.Configure(builder);
    }
}

internal sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("Courses", table =>
        {
            table.HasTrigger("TR_Courses_SetUpdatedAtUtc");
            table.HasCheckConstraint("CK_Courses_PlannedHours", "[planned_hours] > 0");
            table.HasCheckConstraint("CK_Courses_StandardTuition", "[standard_tuition] >= 0");
            table.HasCheckConstraint(
                "CK_Courses_Status",
                "[status] IN ('DRAFT', 'ACTIVE', 'INACTIVE', 'ARCHIVED')");
        });

        builder.HasKey(entity => entity.CourseId).HasName("PK_Courses");
        builder.Property(entity => entity.CourseId).HasColumnName("course_id");
        builder.Property(entity => entity.CourseCode).HasColumnName("course_code").HasMaxLength(20);
        builder.Property(entity => entity.CourseName).HasColumnName("course_name").HasMaxLength(200);
        builder.Property(entity => entity.LevelCode).HasColumnName("level_code").HasMaxLength(20).IsUnicode(false);
        builder.Property(entity => entity.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(entity => entity.PlannedHours).HasColumnName("planned_hours");
        builder.Property(entity => entity.StandardTuition)
            .HasColumnName("standard_tuition")
            .HasColumnType("decimal(19,4)");
        builder.Property(entity => entity.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValueSql("'DRAFT'");

        builder.HasIndex(entity => entity.CourseCode)
            .IsUnique()
            .HasDatabaseName("UQ_Courses_CourseCode");
        builder.HasIndex(entity => new { entity.UpdatedAtUtc, entity.CourseId })
            .HasDatabaseName("IX_Courses_UpdatedAtUtc");

        AuditableEntityConfiguration.Configure(builder);
    }
}

internal sealed class ClassConfiguration : IEntityTypeConfiguration<Class>
{
    public void Configure(EntityTypeBuilder<Class> builder)
    {
        builder.ToTable("Classes", table =>
        {
            table.HasTrigger("TR_Classes_SetUpdatedAtUtc");
            table.HasCheckConstraint("CK_Classes_DateRange", "[end_date] >= [start_date]");
            table.HasCheckConstraint("CK_Classes_Capacity", "[capacity] > 0");
            table.HasCheckConstraint(
                "CK_Classes_Status",
                "[status] IN ('PLANNED', 'OPEN', 'IN_PROGRESS', 'COMPLETED', 'CANCELLED')");
        });

        builder.HasKey(entity => entity.ClassId).HasName("PK_Classes");
        builder.Property(entity => entity.ClassId).HasColumnName("class_id");
        builder.Property(entity => entity.ClassCode).HasColumnName("class_code").HasMaxLength(30);
        builder.Property(entity => entity.CourseId).HasColumnName("course_id");
        builder.Property(entity => entity.TeacherId).HasColumnName("teacher_id");
        builder.Property(entity => entity.ClassName).HasColumnName("class_name").HasMaxLength(200);
        builder.Property(entity => entity.StartDate).HasColumnName("start_date").HasColumnType("date");
        builder.Property(entity => entity.EndDate).HasColumnName("end_date").HasColumnType("date");
        builder.Property(entity => entity.Capacity).HasColumnName("capacity");
        builder.Property(entity => entity.ScheduleNote).HasColumnName("schedule_note").HasMaxLength(500);
        builder.Property(entity => entity.RoomName).HasColumnName("room_name").HasMaxLength(100);
        builder.Property(entity => entity.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValueSql("'PLANNED'");

        builder.HasOne(entity => entity.Course)
            .WithMany(course => course.Classes)
            .HasForeignKey(entity => entity.CourseId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Classes_Courses");
        builder.HasOne(entity => entity.Teacher)
            .WithMany(teacher => teacher.Classes)
            .HasForeignKey(entity => entity.TeacherId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Classes_Teachers");

        builder.HasIndex(entity => entity.ClassCode)
            .IsUnique()
            .HasDatabaseName("UQ_Classes_ClassCode");
        builder.HasIndex(entity => entity.CourseId).HasDatabaseName("IX_Classes_CourseId");
        builder.HasIndex(entity => entity.TeacherId).HasDatabaseName("IX_Classes_TeacherId");
        builder.HasIndex(entity => new { entity.UpdatedAtUtc, entity.ClassId })
            .HasDatabaseName("IX_Classes_UpdatedAtUtc");

        AuditableEntityConfiguration.Configure(builder);
    }
}

internal sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("Enrollments", table =>
        {
            table.HasTrigger("TR_Enrollments_SetUpdatedAtUtc");
            table.HasCheckConstraint("CK_Enrollments_AgreedTuition", "[agreed_tuition] >= 0");
            table.HasCheckConstraint(
                "CK_Enrollments_Status",
                "[status] IN ('PENDING', 'ACTIVE', 'COMPLETED', 'CANCELLED', 'WITHDRAWN')");
        });

        builder.HasKey(entity => entity.EnrollmentId).HasName("PK_Enrollments");
        builder.Property(entity => entity.EnrollmentId).HasColumnName("enrollment_id");
        builder.Property(entity => entity.StudentId).HasColumnName("student_id");
        builder.Property(entity => entity.ClassId).HasColumnName("class_id");
        builder.Property(entity => entity.EnrolledAtUtc)
            .HasColumnName("enrolled_at_utc")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(entity => entity.AgreedTuition)
            .HasColumnName("agreed_tuition")
            .HasColumnType("decimal(19,4)");
        builder.Property(entity => entity.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValueSql("'PENDING'");
        builder.Property(entity => entity.CompletionNote).HasColumnName("completion_note").HasMaxLength(500);

        builder.HasOne(entity => entity.Student)
            .WithMany(student => student.Enrollments)
            .HasForeignKey(entity => entity.StudentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Enrollments_Students");
        builder.HasOne(entity => entity.Class)
            .WithMany(classEntity => classEntity.Enrollments)
            .HasForeignKey(entity => entity.ClassId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Enrollments_Classes");

        builder.HasIndex(entity => new { entity.StudentId, entity.ClassId })
            .IsUnique()
            .HasDatabaseName("UQ_Enrollments_StudentClass");
        builder.HasIndex(entity => new { entity.ClassId, entity.Status })
            .HasDatabaseName("IX_Enrollments_ClassIdStatus");
        builder.HasIndex(entity => new { entity.UpdatedAtUtc, entity.EnrollmentId })
            .HasDatabaseName("IX_Enrollments_UpdatedAtUtc");

        AuditableEntityConfiguration.Configure(builder);
    }
}

internal sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("AttendanceRecords", table =>
        {
            table.HasTrigger("TR_AttendanceRecords_SetUpdatedAtUtc");
            table.HasCheckConstraint(
                "CK_AttendanceRecords_Status",
                "[status] IN ('PRESENT', 'ABSENT', 'LATE', 'EXCUSED')");
        });

        builder.HasKey(entity => entity.AttendanceId).HasName("PK_AttendanceRecords");
        builder.Property(entity => entity.AttendanceId).HasColumnName("attendance_id");
        builder.Property(entity => entity.EnrollmentId).HasColumnName("enrollment_id");
        builder.Property(entity => entity.AttendanceDate).HasColumnName("attendance_date").HasColumnType("date");
        builder.Property(entity => entity.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false);
        builder.Property(entity => entity.CheckInAt)
            .HasColumnName("check_in_at")
            .HasColumnType("datetime2(0)");
        builder.Property(entity => entity.Note).HasColumnName("note").HasMaxLength(500);
        builder.Property(entity => entity.RecordedBy).HasColumnName("recorded_by").HasMaxLength(100);

        builder.HasOne(entity => entity.Enrollment)
            .WithMany(enrollment => enrollment.AttendanceRecords)
            .HasForeignKey(entity => entity.EnrollmentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_AttendanceRecords_Enrollments");

        builder.HasIndex(entity => new { entity.EnrollmentId, entity.AttendanceDate })
            .IsUnique()
            .HasDatabaseName("UQ_AttendanceRecords_EnrollmentDate");
        builder.HasIndex(entity => new { entity.AttendanceDate, entity.EnrollmentId })
            .HasDatabaseName("IX_AttendanceRecords_AttendanceDate");
        builder.HasIndex(entity => new { entity.UpdatedAtUtc, entity.AttendanceId })
            .HasDatabaseName("IX_AttendanceRecords_UpdatedAtUtc");

        AuditableEntityConfiguration.Configure(builder);
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", table =>
        {
            table.HasTrigger("TR_Payments_SetUpdatedAtUtc");
            table.HasCheckConstraint("CK_Payments_Amount", "[amount] > 0");
            table.HasCheckConstraint(
                "CK_Payments_Method",
                "[payment_method] IN ('CASH', 'BANK_TRANSFER', 'CARD', 'E_WALLET', 'OTHER')");
            table.HasCheckConstraint(
                "CK_Payments_Status",
                "[status] IN ('PENDING', 'COMPLETED', 'FAILED', 'REFUNDED', 'CANCELLED')");
        });

        builder.HasKey(entity => entity.PaymentId).HasName("PK_Payments");
        builder.Property(entity => entity.PaymentId).HasColumnName("payment_id");
        builder.Property(entity => entity.EnrollmentId).HasColumnName("enrollment_id");
        builder.Property(entity => entity.PaymentDate).HasColumnName("payment_date").HasColumnType("date");
        builder.Property(entity => entity.Amount).HasColumnName("amount").HasColumnType("decimal(19,4)");
        builder.Property(entity => entity.PaymentMethod)
            .HasColumnName("payment_method")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false);
        builder.Property(entity => entity.PaymentReference)
            .HasColumnName("payment_reference")
            .HasMaxLength(100);
        builder.Property(entity => entity.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValueSql("'PENDING'");
        builder.Property(entity => entity.Note).HasColumnName("note").HasMaxLength(500);

        builder.HasOne(entity => entity.Enrollment)
            .WithMany(enrollment => enrollment.Payments)
            .HasForeignKey(entity => entity.EnrollmentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Payments_Enrollments");

        builder.HasIndex(entity => new { entity.EnrollmentId, entity.PaymentDate })
            .HasDatabaseName("IX_Payments_EnrollmentIdPaymentDate");
        builder.HasIndex(entity => new { entity.UpdatedAtUtc, entity.PaymentId })
            .HasDatabaseName("IX_Payments_UpdatedAtUtc");

        AuditableEntityConfiguration.Configure(builder);
    }
}

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("AppUsers", table =>
            table.HasCheckConstraint(
                "CK_AppUsers_Role",
                "[role] IN ('ADMIN', 'STAFF', 'TEACHER', 'STUDENT')"));

        builder.HasKey(entity => entity.UserId).HasName("PK_AppUsers");
        builder.Property(entity => entity.UserId).HasColumnName("user_id");
        builder.Property(entity => entity.Email).HasColumnName("email").HasMaxLength(320);
        builder.Property(entity => entity.PasswordHash).HasColumnName("password_hash").HasMaxLength(500);
        builder.Property(entity => entity.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false);
        builder.Property(entity => entity.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(entity => entity.StudentId).HasColumnName("student_id");
        builder.Property(entity => entity.TeacherId).HasColumnName("teacher_id");
        builder.Property(entity => entity.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(entity => entity.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(entity => entity.Student)
            .WithOne()
            .HasForeignKey<AppUser>(entity => entity.StudentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_AppUsers_Students");
        builder.HasOne(entity => entity.Teacher)
            .WithOne()
            .HasForeignKey<AppUser>(entity => entity.TeacherId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_AppUsers_Teachers");

        builder.HasIndex(entity => entity.Email).IsUnique().HasDatabaseName("UQ_AppUsers_Email");
        builder.HasIndex(entity => entity.StudentId)
            .IsUnique()
            .HasFilter("[student_id] IS NOT NULL")
            .HasDatabaseName("UQ_AppUsers_StudentId");
        builder.HasIndex(entity => entity.TeacherId)
            .IsUnique()
            .HasFilter("[teacher_id] IS NOT NULL")
            .HasDatabaseName("UQ_AppUsers_TeacherId");
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(entity => entity.RefreshTokenId).HasName("PK_RefreshTokens");
        builder.Property(entity => entity.RefreshTokenId).HasColumnName("refresh_token_id");
        builder.Property(entity => entity.UserId).HasColumnName("user_id");
        builder.Property(entity => entity.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsUnicode(false);
        builder.Property(entity => entity.ExpiresAtUtc).HasColumnName("expires_at_utc").HasColumnType("datetime2(3)");
        builder.Property(entity => entity.RevokedAtUtc).HasColumnName("revoked_at_utc").HasColumnType("datetime2(3)");
        builder.Property(entity => entity.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(entity => entity.User)
            .WithMany(user => user.RefreshTokens)
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_RefreshTokens_AppUsers");

        builder.HasIndex(entity => entity.TokenHash).IsUnique().HasDatabaseName("UQ_RefreshTokens_TokenHash");
        builder.HasIndex(entity => new { entity.UserId, entity.ExpiresAtUtc })
            .HasDatabaseName("IX_RefreshTokens_UserExpiry");
    }
}
