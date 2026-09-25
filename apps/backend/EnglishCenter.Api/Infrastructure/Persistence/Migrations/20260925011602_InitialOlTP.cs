using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishCenter.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialOlTP : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Courses",
                columns: table => new
                {
                    course_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    course_code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    course_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    level_code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    planned_hours = table.Column<short>(type: "smallint", nullable: false),
                    standard_tuition = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValueSql: "'DRAFT'"),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Courses", x => x.course_id);
                    table.CheckConstraint("CK_Courses_PlannedHours", "[planned_hours] > 0");
                    table.CheckConstraint("CK_Courses_StandardTuition", "[standard_tuition] >= 0");
                    table.CheckConstraint("CK_Courses_Status", "[status] IN ('DRAFT', 'ACTIVE', 'INACTIVE', 'ARCHIVED')");
                });

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    student_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    student_code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    full_name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    gender = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    guardian_name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    guardian_phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValueSql: "'ACTIVE'"),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.student_id);
                    table.CheckConstraint("CK_Students_Gender", "[gender] IS NULL OR [gender] IN ('MALE', 'FEMALE', 'OTHER')");
                    table.CheckConstraint("CK_Students_Status", "[status] IN ('ACTIVE', 'INACTIVE', 'GRADUATED', 'SUSPENDED')");
                });

            migrationBuilder.CreateTable(
                name: "Teachers",
                columns: table => new
                {
                    teacher_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    teacher_code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    full_name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    specialization = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValueSql: "'ACTIVE'"),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teachers", x => x.teacher_id);
                    table.CheckConstraint("CK_Teachers_Status", "[status] IN ('ACTIVE', 'INACTIVE', 'ON_LEAVE')");
                });

            migrationBuilder.CreateTable(
                name: "Classes",
                columns: table => new
                {
                    class_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    class_code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    course_id = table.Column<long>(type: "bigint", nullable: false),
                    teacher_id = table.Column<long>(type: "bigint", nullable: true),
                    class_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    capacity = table.Column<short>(type: "smallint", nullable: false),
                    schedule_note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    room_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValueSql: "'PLANNED'"),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Classes", x => x.class_id);
                    table.CheckConstraint("CK_Classes_Capacity", "[capacity] > 0");
                    table.CheckConstraint("CK_Classes_DateRange", "[end_date] >= [start_date]");
                    table.CheckConstraint("CK_Classes_Status", "[status] IN ('PLANNED', 'OPEN', 'IN_PROGRESS', 'COMPLETED', 'CANCELLED')");
                    table.ForeignKey(
                        name: "FK_Classes_Courses",
                        column: x => x.course_id,
                        principalTable: "Courses",
                        principalColumn: "course_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Classes_Teachers",
                        column: x => x.teacher_id,
                        principalTable: "Teachers",
                        principalColumn: "teacher_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Enrollments",
                columns: table => new
                {
                    enrollment_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    student_id = table.Column<long>(type: "bigint", nullable: false),
                    class_id = table.Column<long>(type: "bigint", nullable: false),
                    enrolled_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    agreed_tuition = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValueSql: "'PENDING'"),
                    completion_note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enrollments", x => x.enrollment_id);
                    table.CheckConstraint("CK_Enrollments_AgreedTuition", "[agreed_tuition] >= 0");
                    table.CheckConstraint("CK_Enrollments_Status", "[status] IN ('PENDING', 'ACTIVE', 'COMPLETED', 'CANCELLED', 'WITHDRAWN')");
                    table.ForeignKey(
                        name: "FK_Enrollments_Classes",
                        column: x => x.class_id,
                        principalTable: "Classes",
                        principalColumn: "class_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Enrollments_Students",
                        column: x => x.student_id,
                        principalTable: "Students",
                        principalColumn: "student_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceRecords",
                columns: table => new
                {
                    attendance_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    enrollment_id = table.Column<long>(type: "bigint", nullable: false),
                    attendance_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    check_in_at = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    recorded_by = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRecords", x => x.attendance_id);
                    table.CheckConstraint("CK_AttendanceRecords_Status", "[status] IN ('PRESENT', 'ABSENT', 'LATE', 'EXCUSED')");
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_Enrollments",
                        column: x => x.enrollment_id,
                        principalTable: "Enrollments",
                        principalColumn: "enrollment_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    payment_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    enrollment_id = table.Column<long>(type: "bigint", nullable: false),
                    payment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    payment_method = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    payment_reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValueSql: "'PENDING'"),
                    note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.payment_id);
                    table.CheckConstraint("CK_Payments_Amount", "[amount] > 0");
                    table.CheckConstraint("CK_Payments_Method", "[payment_method] IN ('CASH', 'BANK_TRANSFER', 'CARD', 'E_WALLET', 'OTHER')");
                    table.CheckConstraint("CK_Payments_Status", "[status] IN ('PENDING', 'COMPLETED', 'FAILED', 'REFUNDED', 'CANCELLED')");
                    table.ForeignKey(
                        name: "FK_Payments_Enrollments",
                        column: x => x.enrollment_id,
                        principalTable: "Enrollments",
                        principalColumn: "enrollment_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Courses",
                columns: new[] { "course_id", "course_code", "course_name", "created_at_utc", "description", "level_code", "planned_hours", "standard_tuition", "status", "updated_at_utc" },
                values: new object[] { 1001L, "ENG-A1-DEMO", "English A1 Foundation", new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), "Deterministic local development seed course.", "A1", (short)40, 3000000m, "ACTIVE", new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "Students",
                columns: new[] { "student_id", "created_at_utc", "date_of_birth", "email", "full_name", "gender", "guardian_name", "guardian_phone", "phone", "student_code", "updated_at_utc" },
                values: new object[] { 1001L, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2010, 5, 12), "student.demo@example.test", "Nguyen Minh Anh", "FEMALE", "Nguyen Van Minh", "0900000011", "0900000001", "STU-DEMO-001", new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "Teachers",
                columns: new[] { "teacher_id", "created_at_utc", "email", "full_name", "phone", "specialization", "teacher_code", "updated_at_utc" },
                values: new object[] { 1001L, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), "teacher.demo@example.test", "Tran Thu Ha", "0900000002", "General English", "TCH-DEMO-001", new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "Classes",
                columns: new[] { "class_id", "capacity", "class_code", "class_name", "course_id", "created_at_utc", "end_date", "room_name", "schedule_note", "start_date", "status", "teacher_id", "updated_at_utc" },
                values: new object[] { 1001L, (short)20, "CLS-A1-DEMO-001", "A1 Demo Evening Class", 1001L, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 12, 31), "Room A1", "Tuesday and Thursday, 18:30-20:00", new DateOnly(2026, 10, 1), "OPEN", 1001L, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "Enrollments",
                columns: new[] { "enrollment_id", "agreed_tuition", "class_id", "completion_note", "created_at_utc", "enrolled_at_utc", "status", "student_id", "updated_at_utc" },
                values: new object[] { 1001L, 2900000m, 1001L, null, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), "ACTIVE", 1001L, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "AttendanceRecords",
                columns: new[] { "attendance_id", "attendance_date", "check_in_at", "created_at_utc", "enrollment_id", "note", "recorded_by", "status", "updated_at_utc" },
                values: new object[] { 1001L, new DateOnly(2026, 10, 1), new DateTime(2026, 10, 1, 18, 25, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, null, "seed", "PRESENT", new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "Payments",
                columns: new[] { "payment_id", "amount", "created_at_utc", "enrollment_id", "note", "payment_date", "payment_method", "payment_reference", "status", "updated_at_utc" },
                values: new object[] { 1001L, 1450000m, new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc), 1001L, "Deterministic development seed payment.", new DateOnly(2026, 9, 25), "BANK_TRANSFER", "DEMO-PAYMENT-001", "COMPLETED", new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_AttendanceDate",
                table: "AttendanceRecords",
                columns: new[] { "attendance_date", "enrollment_id" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_UpdatedAtUtc",
                table: "AttendanceRecords",
                columns: new[] { "updated_at_utc", "attendance_id" });

            migrationBuilder.CreateIndex(
                name: "UQ_AttendanceRecords_EnrollmentDate",
                table: "AttendanceRecords",
                columns: new[] { "enrollment_id", "attendance_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Classes_CourseId",
                table: "Classes",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_TeacherId",
                table: "Classes",
                column: "teacher_id");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_UpdatedAtUtc",
                table: "Classes",
                columns: new[] { "updated_at_utc", "class_id" });

            migrationBuilder.CreateIndex(
                name: "UQ_Classes_ClassCode",
                table: "Classes",
                column: "class_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Courses_UpdatedAtUtc",
                table: "Courses",
                columns: new[] { "updated_at_utc", "course_id" });

            migrationBuilder.CreateIndex(
                name: "UQ_Courses_CourseCode",
                table: "Courses",
                column: "course_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_ClassIdStatus",
                table: "Enrollments",
                columns: new[] { "class_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_UpdatedAtUtc",
                table: "Enrollments",
                columns: new[] { "updated_at_utc", "enrollment_id" });

            migrationBuilder.CreateIndex(
                name: "UQ_Enrollments_StudentClass",
                table: "Enrollments",
                columns: new[] { "student_id", "class_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_EnrollmentIdPaymentDate",
                table: "Payments",
                columns: new[] { "enrollment_id", "payment_date" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_UpdatedAtUtc",
                table: "Payments",
                columns: new[] { "updated_at_utc", "payment_id" });

            migrationBuilder.CreateIndex(
                name: "IX_Students_UpdatedAtUtc",
                table: "Students",
                columns: new[] { "updated_at_utc", "student_id" });

            migrationBuilder.CreateIndex(
                name: "UQ_Students_StudentCode",
                table: "Students",
                column: "student_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_UpdatedAtUtc",
                table: "Teachers",
                columns: new[] { "updated_at_utc", "teacher_id" });

            migrationBuilder.CreateIndex(
                name: "UQ_Teachers_TeacherCode",
                table: "Teachers",
                column: "teacher_code",
                unique: true);

            CreateUpdatedAtTrigger(migrationBuilder, "Students", "student_id");
            CreateUpdatedAtTrigger(migrationBuilder, "Teachers", "teacher_id");
            CreateUpdatedAtTrigger(migrationBuilder, "Courses", "course_id");
            CreateUpdatedAtTrigger(migrationBuilder, "Classes", "class_id");
            CreateUpdatedAtTrigger(migrationBuilder, "Enrollments", "enrollment_id");
            CreateUpdatedAtTrigger(migrationBuilder, "AttendanceRecords", "attendance_id");
            CreateUpdatedAtTrigger(migrationBuilder, "Payments", "payment_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceRecords");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "Enrollments");

            migrationBuilder.DropTable(
                name: "Classes");

            migrationBuilder.DropTable(
                name: "Students");

            migrationBuilder.DropTable(
                name: "Courses");

            migrationBuilder.DropTable(
                name: "Teachers");
        }

        private static void CreateUpdatedAtTrigger(
            MigrationBuilder migrationBuilder,
            string tableName,
            string keyColumn)
        {
            var triggerName = $"TR_{tableName}_SetUpdatedAtUtc";

            migrationBuilder.Sql($"""
                CREATE OR ALTER TRIGGER [dbo].[{triggerName}]
                ON [dbo].[{tableName}]
                AFTER UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF TRIGGER_NESTLEVEL() > 1 RETURN;

                    UPDATE [target]
                    SET [updated_at_utc] = SYSUTCDATETIME()
                    FROM [dbo].[{tableName}] AS [target]
                    INNER JOIN [inserted] AS [source]
                        ON [source].[{keyColumn}] = [target].[{keyColumn}];
                END;
                """);
        }
    }
}
