USE [EnglishCenter];
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

INSERT dbo.Students
    (student_code, full_name, date_of_birth, gender, email, phone)
VALUES
    (N'SMOKE-STUDENT', N'Smoke Test Student', '2010-01-15', 'OTHER',
     N'smoke.student@example.test', N'0000000000');
DECLARE @student_id BIGINT = SCOPE_IDENTITY();

INSERT dbo.Teachers
    (teacher_code, full_name, email, specialization)
VALUES
    (N'SMOKE-TEACHER', N'Smoke Test Teacher',
     N'smoke.teacher@example.test', N'English');
DECLARE @teacher_id BIGINT = SCOPE_IDENTITY();

INSERT dbo.Courses
    (course_code, course_name, level_code, planned_hours,
     standard_tuition, status)
VALUES
    (N'SMOKE-COURSE', N'Smoke Test Course', 'A1', 40, 2500000, 'ACTIVE');
DECLARE @course_id BIGINT = SCOPE_IDENTITY();

INSERT dbo.Classes
    (class_code, course_id, teacher_id, class_name, start_date, end_date,
     capacity, status)
VALUES
    (N'SMOKE-CLASS', @course_id, @teacher_id, N'Smoke Test Class',
     '2026-10-01', '2026-12-31', 20, 'OPEN');
DECLARE @class_id BIGINT = SCOPE_IDENTITY();

INSERT dbo.Enrollments
    (student_id, class_id, agreed_tuition, status)
VALUES
    (@student_id, @class_id, 2400000, 'ACTIVE');
DECLARE @enrollment_id BIGINT = SCOPE_IDENTITY();

INSERT dbo.AttendanceRecords
    (enrollment_id, attendance_date, status, recorded_by)
VALUES
    (@enrollment_id, '2026-10-01', 'PRESENT', N'smoke-test');

INSERT dbo.Payments
    (enrollment_id, payment_date, amount, payment_method, status)
VALUES
    (@enrollment_id, '2026-09-24', 1200000, 'BANK_TRANSFER', 'COMPLETED');

DECLARE @before_update DATETIME2(3) =
    (SELECT updated_at_utc FROM dbo.Students WHERE student_id = @student_id);
WAITFOR DELAY '00:00:00.020';
UPDATE dbo.Students
SET phone = N'0000000001'
WHERE student_id = @student_id;

SELECT
    COUNT_BIG(*) AS joined_business_rows,
    MAX(CASE WHEN student.updated_at_utc > @before_update THEN 1 ELSE 0 END)
        AS update_trigger_advanced_timestamp
FROM dbo.Students AS student
INNER JOIN dbo.Enrollments AS enrollment
    ON enrollment.student_id = student.student_id
INNER JOIN dbo.Classes AS class
    ON class.class_id = enrollment.class_id
INNER JOIN dbo.Courses AS course
    ON course.course_id = class.course_id
INNER JOIN dbo.AttendanceRecords AS attendance
    ON attendance.enrollment_id = enrollment.enrollment_id
INNER JOIN dbo.Payments AS payment
    ON payment.enrollment_id = enrollment.enrollment_id
WHERE student.student_code = N'SMOKE-STUDENT';

ROLLBACK TRANSACTION;

SELECT COUNT_BIG(*) AS rows_after_rollback
FROM dbo.Students
WHERE student_code = N'SMOKE-STUDENT';

SET XACT_ABORT OFF;
BEGIN TRY
    INSERT dbo.Courses
        (course_code, course_name, level_code, planned_hours,
         standard_tuition, status)
    VALUES
        (N'INVALID-COURSE', N'Invalid Course', 'A1', 0, 0, 'ACTIVE');
    SELECT CAST(0 AS INT) AS invalid_constraint_rejected;
END TRY
BEGIN CATCH
    SELECT
        CAST(1 AS INT) AS invalid_constraint_rejected,
        ERROR_NUMBER() AS error_number;
END CATCH;
