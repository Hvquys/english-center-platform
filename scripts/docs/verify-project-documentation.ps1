[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

function Assert-True {
    param([Parameter(Mandatory)][bool]$Condition, [Parameter(Mandatory)][string]$Message)
    if (-not $Condition) { throw $Message }
}

function Read-RepositoryFile {
    param([Parameter(Mandatory)][string]$RelativePath)
    $path = Join-Path $repositoryRoot $RelativePath
    Assert-True (Test-Path -LiteralPath $path -PathType Leaf) "Missing required documentation file: $RelativePath"
    return Get-Content -LiteralPath $path -Raw
}

$readme = Read-RepositoryFile 'README.md'
$architecture = Read-RepositoryFile 'docs\architecture\architecture-v2.md'
$mermaid = Read-RepositoryFile 'docs\architecture\architecture-v2.mmd'
$localGuide = Read-RepositoryFile 'docs\LOCAL_DEVELOPMENT.md'
$svgText = Read-RepositoryFile 'docs\architecture\architecture-v2.svg'
$pngPath = Join-Path $repositoryRoot 'docs\architecture\architecture-v2.png'
Assert-True (Test-Path -LiteralPath $pngPath -PathType Leaf) 'Missing rendered architecture PNG preview.'

Assert-True ($readme.Contains('docs/architecture/architecture-v2.svg')) 'README does not embed the Architecture V2 SVG.'
Assert-True ($readme.Contains('docs/LOCAL_DEVELOPMENT.md')) 'README does not link the local development guide.'
Assert-True ($readme.Contains('M1–M5 application core completed')) 'README milestone does not reflect the verified application core.'

foreach ($requiredText in @(
    'SQL Server is the operational database and the **OLTP source of truth**',
    'It is **not** a one-to-one replica',
    'Debezium change-data capture and Kafka streaming are future/advanced options'
)) {
    Assert-True ($architecture.Contains($requiredText)) "Architecture decision is missing: $requiredText"
}
Assert-True ($architecture -match 'current supported development environment is local\s+Docker Compose') `
    'Architecture decision is missing: local Docker Compose is the supported environment.'

foreach ($requiredText in @(
    'SQL Server OLTP',
    'Python incremental ETL',
    'MinIO RAW/Bronze',
    'Spark / PySpark',
    'PostgreSQL DWH',
    'Data Quality + Reconciliation',
    'Debezium',
    'Kafka'
)) {
    Assert-True ($mermaid.Contains($requiredText)) "Mermaid source is missing: $requiredText"
}

foreach ($requiredText in @(
    'docker compose --env-file .\infrastructure\.env',
    'pwsh -NoProfile -File .\scripts\integration\verify-application-core.ps1',
    'pwsh -NoProfile -File .\scripts\docs\verify-project-documentation.ps1',
    '<UseAppHost>false</UseAppHost>',
    'EnglishCenter.Api.dll',
    'Do not add `-v`'
)) {
    Assert-True ($localGuide.Contains($requiredText)) "Local guide is missing: $requiredText"
}

try {
    [xml]$svg = $svgText
}
catch {
    throw "Architecture SVG is not well-formed XML: $($_.Exception.Message)"
}
Assert-True ($svg.DocumentElement.LocalName -eq 'svg') 'Architecture visual root element is not SVG.'
Assert-True ($svgText.Contains('Source of Truth')) 'Architecture SVG does not label SQL Server as Source of Truth.'
Assert-True ($svgText.Contains('not a 1:1 replica')) 'Architecture SVG does not distinguish the PostgreSQL DWH.'
Assert-True ($svgText.Contains('Future / advanced M10')) 'Architecture SVG does not mark M10 as future/advanced.'

$trackedEnvironment = git -C $repositoryRoot ls-files --error-unmatch infrastructure/.env 2>$null
Assert-True ($LASTEXITCODE -ne 0 -and [string]::IsNullOrWhiteSpace($trackedEnvironment)) 'infrastructure/.env must remain untracked.'

[pscustomobject]@{
    Verification = 'PASS'
    RequiredFiles = 'PASS'
    ReadmeLinks = 'PASS'
    ArchitectureBoundaries = 'PASS'
    MermaidSource = 'PASS'
    LocalRunbook = 'PASS'
    SvgStructure = 'PASS'
    SecretFileIgnored = 'PASS'
}
