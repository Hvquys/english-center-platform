[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]{0,127}$')]
    [string]$DatabaseName = 'EnglishCenterEfTest',

    [Parameter()]
    [string]$ContainerName
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$environmentFile = Join-Path $repositoryRoot 'infrastructure\.env'
$verificationFile = Join-Path $repositoryRoot 'data\oltp\sqlserver\tests\verify_ef_migration.sql'
$composeFile = Join-Path $repositoryRoot 'infrastructure\docker-compose.yml'

if (-not (Test-Path -LiteralPath $environmentFile)) {
    throw "Missing $environmentFile."
}

$settings = @{}
foreach ($line in Get-Content -LiteralPath $environmentFile) {
    $trimmedLine = $line.Trim()
    if ($trimmedLine.Length -eq 0 -or $trimmedLine.StartsWith('#')) {
        continue
    }

    $name, $value = $trimmedLine -split '=', 2
    if ($null -ne $value) {
        $settings[$name.Trim()] = $value.Trim()
    }
}

if (-not $settings.ContainsKey('SQLSERVER_SA_PASSWORD') -or [string]::IsNullOrWhiteSpace($settings['SQLSERVER_SA_PASSWORD'])) {
    throw 'Missing SQLSERVER_SA_PASSWORD in infrastructure/.env.'
}

$sql = (Get-Content -LiteralPath $verificationFile -Raw) -replace ':setvar DatabaseName "EnglishCenterEfTest"', ":setvar DatabaseName `"$DatabaseName`""
$passwordArgument = "SQLCMDPASSWORD=$($settings['SQLSERVER_SA_PASSWORD'])"

if ([string]::IsNullOrWhiteSpace($ContainerName)) {
    $ContainerName = (& docker compose --env-file $environmentFile -f $composeFile ps -q sqlserver).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($ContainerName)) {
        throw 'The SQL Server Compose container is not running.'
    }
}

$sql | & docker exec --env $passwordArgument -i $ContainerName /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b
if ($LASTEXITCODE -ne 0) {
    throw 'EF Core database verification failed.'
}
