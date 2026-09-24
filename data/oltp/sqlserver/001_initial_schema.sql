:setvar DatabaseName "EnglishCenter"

IF DB_ID(N'$(DatabaseName)') IS NULL
BEGIN
    EXEC(N'CREATE DATABASE [' + '$(DatabaseName)' + N']');
END;
GO

USE [$(DatabaseName)];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Students', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Students
        (
            student_id BIGINT IDENTITY(1, 1) NOT NULL,
            student_code NVARCHAR(20) NOT NULL,
            full_name NVARCHAR(150) NOT NULL,
            date_of_birth DATE NULL,
            gender VARCHAR(10) NULL,
            email NVARCHAR(320) NULL,
            phone NVARCHAR(30) NULL,
            guardian_name NVARCHAR(150) NULL,
            guardian_phone NVARCHAR(30) NULL,
            status VARCHAR(20) NOT NULL
                CONSTRAINT DF_Students_Status DEFAULT ('ACTIVE'),
            created_at_utc DATETIME2(3) NOT NULL
                CONSTRAINT DF_Students_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
            updated_at_utc DATETIME2(3) NOT NULL
                CONSTRAINT DF_Students_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
            is_deleted BIT NOT NULL
                CONSTRAINT DF_Students_IsDeleted DEFAULT (0),
            row_version ROWVERSION NOT NULL,
            CONSTRAINT PK_Students PRIMARY KEY CLUSTERED (student_id),
            CONSTRAINT UQ_Students_StudentCode UNIQUE (student_code),
            CONSTRAINT CK_Students_Gender
                CHECK (gender IS NULL OR gender IN ('MALE', 'FEMALE', 'OTHER')),
            CONSTRAINT CK_Students_Status
                CHECK (status IN ('ACTIVE', 'INACTIVE', 'GRADUATED', 'SUSPENDED'))
        );
    END;

    IF OBJECT_ID(N'dbo.Teachers', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Teachers
        (
            teacher_id BIGINT IDENTITY(1, 1) NOT NULL,
            teacher_code NVARCHAR(20) NOT NULL,
            full_name NVARCHAR(150) NOT NULL,
            email NVARCHAR(320) NULL,
            phone NVARCHAR(30) NULL,
            specialization NVARCHAR(200) NULL,
            status VARCHAR(20) NOT NULL
                CONSTRAINT DF_Teachers_Status DEFAULT ('ACTIVE'),
            created_at_utc DATETIME2(3) NOT NULL
                CONSTRAINT DF_Teachers_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
            updated_at_utc DATETIME2(3) NOT NULL
                CONSTRAINT DF_Teachers_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
            is_deleted BIT NOT NULL
                CONSTRAINT DF_Teachers_IsDeleted DEFAULT (0),
            row_version ROWVERSION NOT NULL,
            CONSTRAINT PK_Teachers PRIMARY KEY CLUSTERED (teacher_id),
            CONSTRAINT UQ_Teachers_TeacherCode UNIQUE (teacher_code),
            CONSTRAINT CK_Teachers_Status
                CHECK (status IN ('ACTIVE', 'INACTIVE', 'ON_LEAVE'))
        );
    END;

    IF OBJECT_ID(N'dbo.Courses', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Courses
        (
            course_id BIGINT IDENTITY(1, 1) NOT NULL,
            course_code NVARCHAR(20) NOT NULL,
            course_name NVARCHAR(200) NOT NULL,
            level_code VARCHAR(20) NOT NULL,
            description NVARCHAR(1000) NULL,
            planned_hours SMALLINT NOT NULL,
            standard_tuition DECIMAL(19, 4) NOT NULL,
            status VARCHAR(20) NOT NULL
                CONSTRAINT DF_Courses_Status DEFAULT ('DRAFT'),
            created_at_utc DATETIME2(3) NOT NULL
                CONSTRAINT DF_Courses_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
            updated_at_utc DATETIME2(3) NOT NULL
                CONSTRAINT DF_Courses_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
            is_deleted BIT NOT NULL
                CONSTRAINT DF_Courses_IsDeleted DEFAULT (0),
            row_version ROWVERSION NOT NULL,
            CONSTRAINT PK_Courses PRIMARY KEY CLUSTERED (course_id),
            CONSTRAINT UQ_Courses_CourseCode UNIQUE (course_code),
            CONSTRAINT CK_Courses_PlannedHours CHECK (planned_hours > 0),
            CONSTRAINT CK_Courses_StandardTuition CHECK (standard_tuition >= 0),
            CONSTRAINT CK_Courses_Status
                CHECK (status IN ('DRAFT', 'ACTIVE', 'INACTIVE', 'ARCHIVED'))
        );
    END;

    IF OBJECT_ID(N'dbo.Classes', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Classes
        (
            class_id BIGINT IDENTITY(1, 1) NOT NULL,
            class_code NVARCHAR(30) NOT NULL,
            course_id BIGINT NOT NULL,
            teacher_id BIGINT NULL,
            class_name NVARCHAR(200) NOT NULL,
            start_date DATE NOT NULL,
            end_date DATE NOT NULL,
            capacity SMALLINT NOT NULL,
            schedule_note NVARCHAR(500) NULL,
            room_name NVARCHAR(100) NULL,
            status VARCHAR(20) NOT NULL
                CONSTRAINT DF_Classes_Status DEFAULT ('PLANNED'),
            created_at_utc DATETIME2(3) NOT NULL
                CONSTRAINT DF_Classes_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
            updated_at_utc DATETIME2(3) NOT NULL
                CONSTRAINT DF_Classes_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
            is_deleted BIT NOT NULL
                CONSTRAINT DF_Classes_IsDeleted DEFAULT (0),
            row_version ROWVERSION NOT NULL,
            CONSTRAINT PK_Classes PRIMARY KEY CLUSTERED (class_id),
            CONSTRAINT UQ_Classes_ClassCode UNIQUE (class_code),
            CONSTRAINT FK_Classes_Courses
                FOREIGN KEY (course_id) REFERENCES dbo.Courses (course_id),
            CONSTRAINT FK_Classes_Teachers
                FOREIGN KEY (teacher_id) REFERENCES dbo.Teachers (teacher_id),
            CONSTRAINT CK_Classes_DateRange CHECK (end_date >= start_date),
            CONSTRAINT CK_Classes_Capacity CHECK (capacity > 0),
            CONSTRAINT CK_Classes_Status
                CHECK (status IN ('PLANNED', 'OPEN', 'IN_PROGRESS', 'COMPLETED', 'CANCELLED'))
        );
    END;

    IF OBJECT_ID(N'dbo.Enrollments', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Enrollments
        (
            enrollment_id BIGINT IDENTITY(1, 1) NOT NULL,
            student_id BIGINT NOT NULL,
            class_id BIGINT NOT NULL,
            enrolled_at_utc DATETIME2(3) NOT NULL
                CONSTRAINT DF_Enrollments_EnrolledAtUtc DEFAULT (SYSUTCDATETIME()),
            agreed_tuition DECIMAL(19, 4) NOT NULL,
            status VARCHAR(20) NOT NULL
                CONSTRAINT DF_Enrollments_Status DEFAULT ('PENDING'),
            completion_note NVARCHAR(500) NULL,
            created_at_utc DATETIME2(3) NOT NULL
                CONSTRAINT DF_Enrollments_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
            updated_at_utc DATETIME2(3) NOT NULL
                CONSTRAINT DF_Enrollments_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
            is_deleted BIT NOT NULL
                CONSTRAINT DF_Enrollments_IsDeleted DEFAULT (0),
            row_version ROWVERSION NOT NULL,
            CONSTRAINT PK_Enrollments PRIMARY KEY CLUSTERED (enrollment_id),
            CONSTRAINT UQ_Enrollments_StudentClass UNIQUE (student_id, class_id),
            CONSTRAINT FK_Enrollments_Students
                FOREIGN KEY (student_id) REFERENCES dbo.Students (student_id),
            CONSTRAINT FK_Enrollments_Classes
                FOREIGN KEY (class_id) REFERENCES dbo.Classes (class_id),
            CONSTRAINT CK_Enrollments_AgreedTuition CHECK (agreed_tuition >= 0),
            CONSTRAINT CK_Enrollments_Status
                CHECK (status IN ('PENDING', 'ACTIVE', 'COMPLETED', 'CANCELLED', 'WITHDRAWN'))
        );
    END;

    IF OBJECT_ID(N'dbo.AttendanceRecords', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.AttendanceRecords
        (
            attendance_id BIGINT IDENTITY(1, 1) NOT NULL,
            enrollment_id BIGINT NOT NULL,
            attendance_date DATE NOT NULL,
            status VARCHAR(20) NOT NULL,
            check_in_at DATETIME2(0) NULL,
            note NVARCHAR(500) NULL,
            recorded_by NVARCHAR(100) NULL,
            created_at_utc DATETIME2(3) NOT NULL
                CONSTRAINT DF_AttendanceRecords_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
            updated_at_utc DATETIME2(3) NOT NULL
                CONSTRAINT DF_AttendanceRecords_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
            is_deleted BIT NOT NULL
                CONSTRAINT DF_AttendanceRecords_IsDeleted DEFAULT (0),
            row_version ROWVERSION NOT NULL,
            CONSTRAINT PK_AttendanceRecords PRIMARY KEY CLUSTERED (attendance_id),
            CONSTRAINT UQ_AttendanceRecords_EnrollmentDate
                UNIQUE (enrollment_id, attendance_date),
            CONSTRAINT FK_AttendanceRecords_Enrollments
                FOREIGN KEY (enrollment_id) REFERENCES dbo.Enrollments (enrollment_id),
            CONSTRAINT CK_AttendanceRecords_Status
                CHECK (status IN ('PRESENT', 'ABSENT', 'LATE', 'EXCUSED'))
        );
    END;

    IF OBJECT_ID(N'dbo.Payments', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Payments
        (
            payment_id BIGINT IDENTITY(1, 1) NOT NULL,
            enrollment_id BIGINT NOT NULL,
            payment_date DATE NOT NULL,
            amount DECIMAL(19, 4) NOT NULL,
            payment_method VARCHAR(20) NOT NULL,
            payment_reference NVARCHAR(100) NULL,
            status VARCHAR(20) NOT NULL
                CONSTRAINT DF_Payments_Status DEFAULT ('PENDING'),
            note NVARCHAR(500) NULL,
            created_at_utc DATETIME2(3) NOT NULL
                CONSTRAINT DF_Payments_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
            updated_at_utc DATETIME2(3) NOT NULL
                CONSTRAINT DF_Payments_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
            is_deleted BIT NOT NULL
                CONSTRAINT DF_Payments_IsDeleted DEFAULT (0),
            row_version ROWVERSION NOT NULL,
            CONSTRAINT PK_Payments PRIMARY KEY CLUSTERED (payment_id),
            CONSTRAINT FK_Payments_Enrollments
                FOREIGN KEY (enrollment_id) REFERENCES dbo.Enrollments (enrollment_id),
            CONSTRAINT CK_Payments_Amount CHECK (amount > 0),
            CONSTRAINT CK_Payments_Method
                CHECK (payment_method IN ('CASH', 'BANK_TRANSFER', 'CARD', 'E_WALLET', 'OTHER')),
            CONSTRAINT CK_Payments_Status
                CHECK (status IN ('PENDING', 'COMPLETED', 'FAILED', 'REFUNDED', 'CANCELLED'))
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Students_UpdatedAtUtc' AND object_id = OBJECT_ID(N'dbo.Students'))
        CREATE INDEX IX_Students_UpdatedAtUtc ON dbo.Students (updated_at_utc, student_id);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Teachers_UpdatedAtUtc' AND object_id = OBJECT_ID(N'dbo.Teachers'))
        CREATE INDEX IX_Teachers_UpdatedAtUtc ON dbo.Teachers (updated_at_utc, teacher_id);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Courses_UpdatedAtUtc' AND object_id = OBJECT_ID(N'dbo.Courses'))
        CREATE INDEX IX_Courses_UpdatedAtUtc ON dbo.Courses (updated_at_utc, course_id);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Classes_CourseId' AND object_id = OBJECT_ID(N'dbo.Classes'))
        CREATE INDEX IX_Classes_CourseId ON dbo.Classes (course_id);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Classes_TeacherId' AND object_id = OBJECT_ID(N'dbo.Classes'))
        CREATE INDEX IX_Classes_TeacherId ON dbo.Classes (teacher_id);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Classes_UpdatedAtUtc' AND object_id = OBJECT_ID(N'dbo.Classes'))
        CREATE INDEX IX_Classes_UpdatedAtUtc ON dbo.Classes (updated_at_utc, class_id);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Enrollments_ClassIdStatus' AND object_id = OBJECT_ID(N'dbo.Enrollments'))
        CREATE INDEX IX_Enrollments_ClassIdStatus ON dbo.Enrollments (class_id, status);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Enrollments_UpdatedAtUtc' AND object_id = OBJECT_ID(N'dbo.Enrollments'))
        CREATE INDEX IX_Enrollments_UpdatedAtUtc ON dbo.Enrollments (updated_at_utc, enrollment_id);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AttendanceRecords_AttendanceDate' AND object_id = OBJECT_ID(N'dbo.AttendanceRecords'))
        CREATE INDEX IX_AttendanceRecords_AttendanceDate ON dbo.AttendanceRecords (attendance_date, enrollment_id);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AttendanceRecords_UpdatedAtUtc' AND object_id = OBJECT_ID(N'dbo.AttendanceRecords'))
        CREATE INDEX IX_AttendanceRecords_UpdatedAtUtc ON dbo.AttendanceRecords (updated_at_utc, attendance_id);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Payments_EnrollmentIdPaymentDate' AND object_id = OBJECT_ID(N'dbo.Payments'))
        CREATE INDEX IX_Payments_EnrollmentIdPaymentDate ON dbo.Payments (enrollment_id, payment_date);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Payments_UpdatedAtUtc' AND object_id = OBJECT_ID(N'dbo.Payments'))
        CREATE INDEX IX_Payments_UpdatedAtUtc ON dbo.Payments (updated_at_utc, payment_id);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

CREATE OR ALTER TRIGGER dbo.TR_Students_SetUpdatedAtUtc
ON dbo.Students
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF TRIGGER_NESTLEVEL() > 1 RETURN;
    UPDATE target
    SET updated_at_utc = SYSUTCDATETIME()
    FROM dbo.Students AS target
    INNER JOIN inserted AS source ON source.student_id = target.student_id;
END;
GO

CREATE OR ALTER TRIGGER dbo.TR_Teachers_SetUpdatedAtUtc
ON dbo.Teachers
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF TRIGGER_NESTLEVEL() > 1 RETURN;
    UPDATE target
    SET updated_at_utc = SYSUTCDATETIME()
    FROM dbo.Teachers AS target
    INNER JOIN inserted AS source ON source.teacher_id = target.teacher_id;
END;
GO

CREATE OR ALTER TRIGGER dbo.TR_Courses_SetUpdatedAtUtc
ON dbo.Courses
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF TRIGGER_NESTLEVEL() > 1 RETURN;
    UPDATE target
    SET updated_at_utc = SYSUTCDATETIME()
    FROM dbo.Courses AS target
    INNER JOIN inserted AS source ON source.course_id = target.course_id;
END;
GO

CREATE OR ALTER TRIGGER dbo.TR_Classes_SetUpdatedAtUtc
ON dbo.Classes
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF TRIGGER_NESTLEVEL() > 1 RETURN;
    UPDATE target
    SET updated_at_utc = SYSUTCDATETIME()
    FROM dbo.Classes AS target
    INNER JOIN inserted AS source ON source.class_id = target.class_id;
END;
GO

CREATE OR ALTER TRIGGER dbo.TR_Enrollments_SetUpdatedAtUtc
ON dbo.Enrollments
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF TRIGGER_NESTLEVEL() > 1 RETURN;
    UPDATE target
    SET updated_at_utc = SYSUTCDATETIME()
    FROM dbo.Enrollments AS target
    INNER JOIN inserted AS source ON source.enrollment_id = target.enrollment_id;
END;
GO

CREATE OR ALTER TRIGGER dbo.TR_AttendanceRecords_SetUpdatedAtUtc
ON dbo.AttendanceRecords
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF TRIGGER_NESTLEVEL() > 1 RETURN;
    UPDATE target
    SET updated_at_utc = SYSUTCDATETIME()
    FROM dbo.AttendanceRecords AS target
    INNER JOIN inserted AS source ON source.attendance_id = target.attendance_id;
END;
GO

CREATE OR ALTER TRIGGER dbo.TR_Payments_SetUpdatedAtUtc
ON dbo.Payments
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF TRIGGER_NESTLEVEL() > 1 RETURN;
    UPDATE target
    SET updated_at_utc = SYSUTCDATETIME()
    FROM dbo.Payments AS target
    INNER JOIN inserted AS source ON source.payment_id = target.payment_id;
END;
GO
