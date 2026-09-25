:setvar DatabaseName "EnglishCenterEfTest"

USE [$(DatabaseName)];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @ExpectedTables int = 7;
DECLARE @ExpectedForeignKeys int = 6;
DECLARE @ExpectedCheckConstraints int = 15;
DECLARE @ExpectedTriggers int = 7;
DECLARE @ExpectedMigrationRows int = 1;

DECLARE @ActualTables int = (
    SELECT COUNT(*)
    FROM sys.tables
    WHERE name IN (
        'Students', 'Teachers', 'Courses', 'Classes',
        'Enrollments', 'AttendanceRecords', 'Payments'
    )
);

DECLARE @ActualForeignKeys int = (
    SELECT COUNT(*)
    FROM sys.foreign_keys
    WHERE parent_object_id IN (
        OBJECT_ID('dbo.Classes'),
        OBJECT_ID('dbo.Enrollments'),
        OBJECT_ID('dbo.AttendanceRecords'),
        OBJECT_ID('dbo.Payments')
    )
);

DECLARE @ActualCheckConstraints int = (
    SELECT COUNT(*)
    FROM sys.check_constraints
    WHERE parent_object_id IN (
        OBJECT_ID('dbo.Students'),
        OBJECT_ID('dbo.Teachers'),
        OBJECT_ID('dbo.Courses'),
        OBJECT_ID('dbo.Classes'),
        OBJECT_ID('dbo.Enrollments'),
        OBJECT_ID('dbo.AttendanceRecords'),
        OBJECT_ID('dbo.Payments')
    )
);

DECLARE @ActualTriggers int = (
    SELECT COUNT(*)
    FROM sys.triggers
    WHERE parent_id IN (
        OBJECT_ID('dbo.Students'),
        OBJECT_ID('dbo.Teachers'),
        OBJECT_ID('dbo.Courses'),
        OBJECT_ID('dbo.Classes'),
        OBJECT_ID('dbo.Enrollments'),
        OBJECT_ID('dbo.AttendanceRecords'),
        OBJECT_ID('dbo.Payments')
    )
);

DECLARE @ActualMigrationRows int = (
    SELECT COUNT(*) FROM dbo.__EFMigrationsHistory
);

IF @ActualTables <> @ExpectedTables
    THROW 51001, 'EF verification failed: expected 7 domain tables.', 1;
IF @ActualForeignKeys <> @ExpectedForeignKeys
    THROW 51002, 'EF verification failed: expected 6 foreign keys.', 1;
IF @ActualCheckConstraints <> @ExpectedCheckConstraints
    THROW 51003, 'EF verification failed: expected 15 check constraints.', 1;
IF @ActualTriggers <> @ExpectedTriggers
    THROW 51004, 'EF verification failed: expected 7 update triggers.', 1;
IF @ActualMigrationRows <> @ExpectedMigrationRows
    THROW 51005, 'EF verification failed: expected 1 migration history row.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.Students AS s
    INNER JOIN dbo.Enrollments AS e ON e.student_id = s.student_id
    INNER JOIN dbo.Classes AS c ON c.class_id = e.class_id
    INNER JOIN dbo.Courses AS co ON co.course_id = c.course_id
    INNER JOIN dbo.Teachers AS t ON t.teacher_id = c.teacher_id
    INNER JOIN dbo.AttendanceRecords AS a ON a.enrollment_id = e.enrollment_id
    INNER JOIN dbo.Payments AS p ON p.enrollment_id = e.enrollment_id
    WHERE s.student_id = 1001
      AND t.teacher_id = 1001
      AND co.course_id = 1001
      AND c.class_id = 1001
      AND e.enrollment_id = 1001
      AND a.attendance_id = 1001
      AND p.payment_id = 1001
)
    THROW 51006, 'EF verification failed: deterministic seed path is incomplete.', 1;

BEGIN TRANSACTION;

DECLARE @UpdatedAtBefore datetime2(3);
DECLARE @RowVersionBefore varbinary(8);

SELECT
    @UpdatedAtBefore = updated_at_utc,
    @RowVersionBefore = row_version
FROM dbo.Students
WHERE student_id = 1001;

WAITFOR DELAY '00:00:00.010';

UPDATE dbo.Students
SET phone = '0900000001'
WHERE student_id = 1001;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.Students
    WHERE student_id = 1001
      AND updated_at_utc > @UpdatedAtBefore
      AND row_version <> @RowVersionBefore
)
    THROW 51007, 'EF verification failed: audit trigger or rowversion did not change.', 1;

ROLLBACK TRANSACTION;

SELECT
    'PASS' AS verification_status,
    @ActualTables AS domain_tables,
    @ActualForeignKeys AS foreign_keys,
    @ActualCheckConstraints AS check_constraints,
    @ActualTriggers AS update_triggers,
    @ActualMigrationRows AS migration_history_rows,
    1 AS complete_seed_paths,
    1 AS audit_update_checks;
GO
