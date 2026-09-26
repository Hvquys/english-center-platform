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

function Invoke-ExpectedStatus {
    param(
        [Parameter(Mandatory)][string]$Uri,
        [Parameter(Mandatory)][ValidateSet('GET', 'POST')][string]$Method,
        [Parameter(Mandatory)][int]$ExpectedStatus,
        [Parameter()][string]$Body,
        [Parameter()][hashtable]$Headers
    )

    try {
        $parameters = @{ Uri = $Uri; Method = $Method; UseBasicParsing = $true }
        if ($Body) { $parameters.Body = $Body; $parameters.ContentType = 'application/json' }
        if ($Headers) { $parameters.Headers = $Headers }
        $response = Invoke-WebRequest @parameters
    }
    catch [Microsoft.PowerShell.Commands.HttpResponseException] {
        $response = $_.Exception.Response
    }

    Assert-True ($response.StatusCode -eq $ExpectedStatus) "Expected HTTP $ExpectedStatus from $Uri, received $($response.StatusCode)."
    return $response
}

$health = Invoke-ExpectedStatus -Uri "$WebBaseUrl/health" -Method GET -ExpectedStatus 200
$healthText = if ($health.Content -is [byte[]]) {
    [System.Text.Encoding]::UTF8.GetString($health.Content).Trim()
} else {
    $health.Content.Trim()
}
Assert-True ($healthText -eq 'Healthy') 'Web health endpoint did not return Healthy.'

$loginPage = Invoke-ExpectedStatus -Uri "$WebBaseUrl/login" -Method GET -ExpectedStatus 200
Assert-True ($loginPage.Content -match '<div id="root"></div>') 'Login route did not return the SPA shell.'

$dashboardPage = Invoke-ExpectedStatus -Uri "$WebBaseUrl/dashboard" -Method GET -ExpectedStatus 200
Assert-True ($dashboardPage.Content -match '<div id="root"></div>') 'Direct dashboard route did not use the SPA fallback.'

$apiHealth = Invoke-ExpectedStatus -Uri "$WebBaseUrl/api/health" -Method GET -ExpectedStatus 200
$apiHealthBody = $apiHealth.Content | ConvertFrom-Json
Assert-True ($apiHealthBody.status -eq 'Healthy') 'API proxy health did not report Healthy.'

$invalidBody = @{ email = 'invalid@example.test'; password = 'InvalidPassword123!' } | ConvertTo-Json
$invalidLogin = Invoke-ExpectedStatus -Uri "$WebBaseUrl/api/auth/login" -Method POST -ExpectedStatus 401 -Body $invalidBody
Assert-True ($invalidLogin.Content.Headers.ContentType -match 'application/problem\+json') 'Invalid login did not return Problem Details.'

$email = Get-LocalEnvironmentValue 'AUTH_BOOTSTRAP_ADMIN_EMAIL'
$password = Get-LocalEnvironmentValue 'AUTH_BOOTSTRAP_ADMIN_PASSWORD'
$loginBody = @{ email = $email; password = $password } | ConvertTo-Json
$loginResponse = Invoke-ExpectedStatus -Uri "$WebBaseUrl/api/auth/login" -Method POST -ExpectedStatus 200 -Body $loginBody
$session = $loginResponse.Content | ConvertFrom-Json
Assert-True (-not [string]::IsNullOrWhiteSpace($session.accessToken)) 'Login did not return an access token.'
Assert-True (-not [string]::IsNullOrWhiteSpace($session.refreshToken)) 'Login did not return a refresh token.'
Assert-True ($session.user.role -eq 'ADMIN') 'Bootstrap login did not return the ADMIN role.'

$authorization = @{ Authorization = "Bearer $($session.accessToken)" }
$meResponse = Invoke-ExpectedStatus -Uri "$WebBaseUrl/api/auth/me" -Method GET -ExpectedStatus 200 -Headers $authorization
$me = $meResponse.Content | ConvertFrom-Json
Assert-True ($me.email -eq $email) '/api/auth/me did not return the logged-in administrator.'

[pscustomobject]@{
    Verification       = 'PASS'
    WebHealth          = 'PASS'
    LoginSpaFallback   = 'PASS'
    DirectRouteFallback = 'PASS'
    ApiProxyHealth     = 'PASS'
    InvalidLogin401    = 'PASS'
    AdminLogin         = 'PASS'
    AuthenticatedMe    = 'PASS'
}
