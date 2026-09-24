USE [EnglishCenter];
SET NOCOUNT ON;

SELECT name
FROM sys.tables
WHERE schema_id = SCHEMA_ID(N'dbo')
  AND name IN
      (N'Students', N'Teachers', N'Courses', N'Classes',
       N'Enrollments', N'AttendanceRecords', N'Payments')
ORDER BY name;

SELECT
    (SELECT COUNT(*) FROM sys.foreign_keys
     WHERE parent_object_id IN
           (OBJECT_ID(N'dbo.Classes'), OBJECT_ID(N'dbo.Enrollments'),
            OBJECT_ID(N'dbo.AttendanceRecords'), OBJECT_ID(N'dbo.Payments')))
        AS foreign_keys,
    (SELECT COUNT(*) FROM sys.check_constraints
     WHERE parent_object_id IN
           (OBJECT_ID(N'dbo.Students'), OBJECT_ID(N'dbo.Teachers'),
            OBJECT_ID(N'dbo.Courses'), OBJECT_ID(N'dbo.Classes'),
            OBJECT_ID(N'dbo.Enrollments'),
            OBJECT_ID(N'dbo.AttendanceRecords'), OBJECT_ID(N'dbo.Payments')))
        AS check_constraints,
    (SELECT COUNT(*) FROM sys.triggers
     WHERE name LIKE N'TR[_]%[_]SetUpdatedAtUtc')
        AS update_triggers;
