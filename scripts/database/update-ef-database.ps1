[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]{0,127}$')]
    [string]$DatabaseName = 'EnglishCenterEfTest'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$environmentFile = Join-Path $repositoryRoot 'infrastructure\.env'
$projectFile = Join-Path $repositoryRoot 'apps\backend\EnglishCenter.Api\EnglishCenter.Api.csproj'

if (-not (Test-Path -LiteralPath $environmentFile)) {
    throw "Missing $environmentFile. Copy infrastructure/.env.example to infrastructure/.env and fill in the local values first."
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

foreach ($requiredName in 'SQLSERVER_SA_PASSWORD') {
    if (-not $settings.ContainsKey($requiredName) -or [string]::IsNullOrWhiteSpace($settings[$requiredName])) {
        throw "Missing $requiredName in infrastructure/.env."
    }
}

$previousConnectionString = [Environment]::GetEnvironmentVariable('ConnectionStrings__SqlServer', 'Process')
$connectionString = "Server=localhost,1433;Database=$DatabaseName;User Id=sa;Password=$($settings['SQLSERVER_SA_PASSWORD']);TrustServerCertificate=True;Encrypt=False"

try {
    [Environment]::SetEnvironmentVariable('ConnectionStrings__SqlServer', $connectionString, 'Process')

    Push-Location $repositoryRoot
    try {
        & dotnet tool restore
        if ($LASTEXITCODE -ne 0) {
            throw 'dotnet tool restore failed.'
        }

        & dotnet tool run dotnet-ef database update --project $projectFile --startup-project $projectFile
        if ($LASTEXITCODE -ne 0) {
            throw 'EF Core database update failed.'
        }
    }
    finally {
        Pop-Location
    }
}
finally {
    [Environment]::SetEnvironmentVariable('ConnectionStrings__SqlServer', $previousConnectionString, 'Process')
}

