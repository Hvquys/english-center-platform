[CmdletBinding()]
param(
    [ValidatePattern('^https?://[^\s/]+(?::\d+)?$')]
    [string]$WebBaseUrl = 'http://localhost:5173'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\api\auth-test-support.ps1')

function Assert-True {
    param([Parameter(Mandatory)][bool]$Condition, [Parameter(Mandatory)][string]$Message)
    if (-not $Condition) { throw $Message }
}

function Invoke-Get {
    param([Parameter(Mandatory)][string]$Uri, [Parameter()][hashtable]$Headers)
    $parameters = @{ Uri = $Uri; Method = 'GET'; UseBasicParsing = $true }
    if ($Headers) { $parameters.Headers = $Headers }
    return Invoke-WebRequest @parameters
}

$routes = @('students', 'classes', 'enrollments', 'attendance', 'payments')
foreach ($route in $routes) {
    $response = Invoke-Get "$WebBaseUrl/$route"
    Assert-True ($response.StatusCode -eq 200) "Route /$route did not return HTTP 200."
    Assert-True ($response.Content -match '<div id="root"></div>') "Route /$route did not return the SPA shell."
}

$email = Get-LocalEnvironmentValue 'AUTH_BOOTSTRAP_ADMIN_EMAIL'
$password = Get-LocalEnvironmentValue 'AUTH_BOOTSTRAP_ADMIN_PASSWORD'
$login = Invoke-RestMethod -Uri "$WebBaseUrl/api/auth/login" -Method Post -ContentType 'application/json' -Body (@{
    email = $email
    password = $password
} | ConvertTo-Json)
$headers = @{ Authorization = "Bearer $($login.accessToken)" }

$resources = @('students', 'classes', 'enrollments', 'attendance', 'payments')
foreach ($resource in $resources) {
    $page = Invoke-RestMethod -Uri "$WebBaseUrl/api/${resource}?page=1&pageSize=5" -Method Get -Headers $headers
    Assert-True ($null -ne $page.items) "API /api/$resource did not return a paged items collection."
    Assert-True ($page.page -eq 1) "API /api/$resource did not preserve the requested page."
    Assert-True ($page.pageSize -eq 5) "API /api/$resource did not preserve the requested page size."
}

[pscustomobject]@{
    Verification       = 'PASS'
    BusinessSpaRoutes  = 'PASS'
    AdminAuthentication = 'PASS'
    StudentApi         = 'PASS'
    ClassApi           = 'PASS'
    EnrollmentApi      = 'PASS'
    AttendanceApi      = 'PASS'
    PaymentApi         = 'PASS'
}
