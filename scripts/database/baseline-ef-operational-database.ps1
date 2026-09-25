[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]{0,127}$')]
    [string]$DatabaseName = 'EnglishCenter'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$composeFile = Join-Path $repositoryRoot 'infrastructure\docker-compose.yml'
$environmentFile = Join-Path $repositoryRoot 'infrastructure\.env'

if (-not (Test-Path -LiteralPath $environmentFile)) {
    throw 'Missing infrastructure/.env.'
}

$sql = @'
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @ExpectedTables int = (
    SELECT COUNT(*)
    FROM sys.tables
    WHERE [name] IN ('Students', 'Teachers', 'Courses', 'Classes', 'Enrollments', 'AttendanceRecords', 'Payments')
);

IF @ExpectedTables <> 7
    THROW 51020, 'Operational schema is incomplete; refusing to baseline EF migration history.', 1;

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925011602_InitialOlTP'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260925011602_InitialOlTP', N'10.0.12');
END;

COMMIT TRANSACTION;
SELECT [MigrationId], [ProductVersion] FROM [__EFMigrationsHistory] ORDER BY [MigrationId];
'@

Push-Location $repositoryRoot
try {
    $sqlCommand = "/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P ""`$MSSQL_SA_PASSWORD"" -d $DatabaseName -b -i /dev/stdin"
    $sql | & docker compose --env-file $environmentFile -f $composeFile exec -T sqlserver `
        /bin/bash -lc $sqlCommand
    if ($LASTEXITCODE -ne 0) { throw 'Failed to baseline EF migration history.' }
}
finally {
    Pop-Location
}
