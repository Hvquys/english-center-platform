# SQL Server OLTP design

## Purpose

SQL Server is the operational database and source of truth for the English
Center Platform. It owns current business state and transactional rules.
PostgreSQL is a separate analytical data warehouse populated through the M6
pipeline; it is not a one-to-one replica of this schema.

The initial schema is implemented in:

    data/oltp/sqlserver/001_initial_schema.sql

Run the script from the repository root after the core Docker Compose stack is
healthy:

~~~powershell
Get-Content -Raw ".\data\oltp\sqlserver\001_initial_schema.sql" |
  docker compose --env-file ".\infrastructure\.env" -f ".\infrastructure\docker-compose.yml" exec -T sqlserver /bin/bash -lc '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -b -i /dev/stdin'
~~~

The password is expanded inside the container from MSSQL_SA_PASSWORD; it is
not stored in this document or the SQL script.

## Transactional model

| Table | Grain | Main relationships |
| --- | --- | --- |
| Students | One current student profile | Parent of Enrollments |
| Teachers | One current teacher profile | Optional teacher of Classes |
| Courses | One reusable course definition | Parent of Classes |
| Classes | One scheduled offering of a course | Belongs to one course and optionally one teacher |
| Enrollments | One student enrolled in one class | Belongs to one student and one class |
| AttendanceRecords | One attendance result per enrollment and date | Belongs to one enrollment |
| Payments | One payment event for an enrollment | Belongs to one enrollment |

The relationship path for reporting is:

    Students -> Enrollments -> Classes -> Courses
                             -> AttendanceRecords
                             -> Payments
    Teachers -> Classes

## Keys and business uniqueness

- All primary keys use BIGINT IDENTITY.
- Business codes are unique: student_code, teacher_code, course_code, and
  class_code.
- A student may enroll in a class only once through
  UQ_Enrollments_StudentClass.
- An enrollment may have one attendance result for a date through
  UQ_AttendanceRecords_EnrollmentDate.
- Foreign keys do not use cascade delete. Operational records are retained and
  logically removed with is_deleted.

## Status rules

Status values are constrained in SQL Server so invalid strings cannot enter
the source of truth.

| Table | Allowed values |
| --- | --- |
| Students | ACTIVE, INACTIVE, GRADUATED, SUSPENDED |
| Teachers | ACTIVE, INACTIVE, ON_LEAVE |
| Courses | DRAFT, ACTIVE, INACTIVE, ARCHIVED |
| Classes | PLANNED, OPEN, IN_PROGRESS, COMPLETED, CANCELLED |
| Enrollments | PENDING, ACTIVE, COMPLETED, CANCELLED, WITHDRAWN |
| AttendanceRecords | PRESENT, ABSENT, LATE, EXCUSED |
| Payments | PENDING, COMPLETED, FAILED, REFUNDED, CANCELLED |

Money uses DECIMAL(19,4) and is constrained to non-negative tuition and
positive payment amounts. Class capacity and planned course hours must be
positive. A class end date cannot be earlier than its start date.

## Change tracking for the M6 pipeline

Every table contains:

- created_at_utc: when the row was inserted;
- updated_at_utc: refreshed by an AFTER UPDATE trigger;
- is_deleted: logical deletion marker;
- row_version: SQL Server ROWVERSION value that changes on update.

The incremental extraction contract is:

1. Extract rows ordered by row_version, using a persisted binary watermark.
2. Include is_deleted so the analytical pipeline can apply tombstones.
3. Store the source primary key and source row_version in RAW/Bronze.
4. Make downstream loads idempotent by source primary key and source version.
5. Use updated_at_utc for readable audit and reconciliation windows.

The exact M6 implementation and watermark storage are deferred to the data
pipeline milestone.

## Index strategy

- Every foreign key used for joins has a supporting index or starts a unique
  index.
- Operational lookup indexes cover class/course/teacher, enrollment/class
  status, attendance date, and payment history.
- Every table has an (updated_at_utc, primary_key) index for time-window
  reconciliation and fallback incremental extraction.
- DWH-specific star-schema indexes do not belong in the OLTP database.

## Verification queries

Repeatable verification scripts are stored in:

    data/oltp/sqlserver/tests/verify_initial_schema.sql
    data/oltp/sqlserver/tests/initial_schema_smoke.sql

Run either file from the repository root with the same secret-safe pattern:

~~~powershell
Get-Content -Raw ".\data\oltp\sqlserver\tests\verify_initial_schema.sql" |
  docker compose --env-file ".\infrastructure\.env" -f ".\infrastructure\docker-compose.yml" exec -T sqlserver /bin/bash -lc '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -b -i /dev/stdin'
~~~

The smoke test inserts a complete business path inside a transaction, verifies
the update trigger, rolls the transaction back, confirms that no test row
remains, and verifies that an invalid course is rejected by a check constraint.

List the seven domain tables:

~~~sql
SELECT name
FROM sys.tables
WHERE schema_id = SCHEMA_ID(N'dbo')
  AND name IN
      (N'Students', N'Teachers', N'Courses', N'Classes',
       N'Enrollments', N'AttendanceRecords', N'Payments')
ORDER BY name;
~~~

Check foreign keys, check constraints, and update triggers:

~~~sql
SELECT
    (SELECT COUNT(*) FROM sys.foreign_keys
     WHERE parent_object_id IN
           (OBJECT_ID(N'dbo.Classes'), OBJECT_ID(N'dbo.Enrollments'),
            OBJECT_ID(N'dbo.AttendanceRecords'), OBJECT_ID(N'dbo.Payments')))
        AS foreign_key_count,
    (SELECT COUNT(*) FROM sys.check_constraints
     WHERE parent_object_id IN
           (OBJECT_ID(N'dbo.Students'), OBJECT_ID(N'dbo.Teachers'),
            OBJECT_ID(N'dbo.Courses'), OBJECT_ID(N'dbo.Classes'),
            OBJECT_ID(N'dbo.Enrollments'),
            OBJECT_ID(N'dbo.AttendanceRecords'), OBJECT_ID(N'dbo.Payments')))
        AS check_constraint_count,
    (SELECT COUNT(*) FROM sys.triggers
     WHERE name LIKE N'TR[_]%[_]SetUpdatedAtUtc')
        AS update_trigger_count;
~~~

Expected counts for the initial schema are 6 foreign keys, 15 check
constraints, and 7 update triggers.

## Scope boundary

This task defines and verifies the database contract. EF Core entity
configuration, migrations, and seed data belong to TASK-004. Business APIs,
authentication, data extraction, DWH dimensions/facts, and CDC/streaming are
implemented in their corresponding later tasks. Debezium and Kafka remain
future/advanced M10 work.
