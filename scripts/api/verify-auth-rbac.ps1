[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^https?://[^\s/]+(?::\d+)?$')]
    [string]$BaseUrl = 'http://localhost:8080'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'auth-test-support.ps1')

function Invoke-Api {
    param(
        [Parameter(Mandatory)][string]$Uri,
        [Parameter(Mandatory)][ValidateSet('GET', 'POST', 'DELETE')][string]$Method,
        [Parameter()][object]$Body,
        [Parameter()][string]$Token,
        [Parameter()][hashtable]$Headers
    )
    $request = @{ UseBasicParsing = $true; Uri = $Uri; Method = $Method }
    $mergedHeaders = @{}
    if (-not [string]::IsNullOrWhiteSpace($Token)) { $mergedHeaders.Authorization = "Bearer $Token" }
    if ($null -ne $Headers) { foreach ($key in $Headers.Keys) { $mergedHeaders[$key] = $Headers[$key] } }
    if ($mergedHeaders.Count -gt 0) { $request.Headers = $mergedHeaders }
    if ($PSBoundParameters.ContainsKey('Body')) {
        $request.Body = $Body | ConvertTo-Json -Depth 8
        $request.ContentType = 'application/json'
    }
    return Invoke-WebRequest @request
}

function Invoke-ExpectedAuthProblem {
    param(
        [Parameter(Mandatory)][string]$Uri,
        [Parameter(Mandatory)][ValidateSet('GET', 'POST')][string]$Method,
        [Parameter(Mandatory)][int]$ExpectedStatus,
        [Parameter()][object]$Body,
        [Parameter()][string]$Token
    )
    try {
        $parameters = @{ Uri = $Uri; Method = $Method }
        if ($PSBoundParameters.ContainsKey('Body')) { $parameters.Body = $Body }
        if (-not [string]::IsNullOrWhiteSpace($Token)) { $parameters.Token = $Token }
        Invoke-Api @parameters | Out-Null
        throw "Expected HTTP $ExpectedStatus from $Method $Uri, but the request succeeded."
    }
    catch [Microsoft.PowerShell.Commands.HttpResponseException] {
        $response = $_.Exception.Response
        if ([int]$response.StatusCode -ne $ExpectedStatus) {
            throw "Expected HTTP $ExpectedStatus from $Method $Uri, received $([int]$response.StatusCode)."
        }
        if ($response.Content.Headers.ContentType.MediaType -ne 'application/problem+json') {
            throw "Expected application/problem+json from $Method $Uri."
        }
        $problem = $_.ErrorDetails.Message | ConvertFrom-Json
        if ($problem.status -ne $ExpectedStatus -or [string]::IsNullOrWhiteSpace($problem.traceId)) {
            throw "ProblemDetails from $Method $Uri is missing status or traceId."
        }
    }
}

function Convert-ResponseJson { param($Response) return $Response.Content | ConvertFrom-Json }
function Assert-True { param([bool]$Condition, [string]$Message) if (-not $Condition) { throw $Message } }

$adminEmail = Get-LocalEnvironmentValue 'AUTH_BOOTSTRAP_ADMIN_EMAIL'
$adminPassword = Get-LocalEnvironmentValue 'AUTH_BOOTSTRAP_ADMIN_PASSWORD'
$suffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds().ToString()
$testPassword = [Convert]::ToBase64String(
    [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(24)) + 'Aa1!'
$createdUserIds = [System.Collections.Generic.List[long]]::new()
$student = $null
$teacher = $null

Invoke-ExpectedAuthProblem "$BaseUrl/api/students" GET 401
Invoke-ExpectedAuthProblem "$BaseUrl/api/auth/login" POST 401 @{ email = $adminEmail; password = 'invalid-password-value' }

$adminLogin = Convert-ResponseJson (Invoke-Api "$BaseUrl/api/auth/login" POST @{
    email = $adminEmail; password = $adminPassword
})
$adminToken = $adminLogin.accessToken
Assert-True ($adminLogin.user.role -eq 'ADMIN') 'Bootstrap account did not receive ADMIN role.'
$adminMe = Convert-ResponseJson (Invoke-Api "$BaseUrl/api/auth/me" GET -Token $adminToken)
Assert-True ($adminMe.email -eq $adminEmail.ToLowerInvariant()) 'Admin /me returned the wrong user.'

try {
    $teacher = Convert-ResponseJson (Invoke-Api "$BaseUrl/api/teachers" POST @{
        teacherCode = "AUTH-T-$suffix"; fullName = 'Auth Acceptance Teacher'; email = "auth-t-$suffix@example.test"
    } -Token $adminToken)
    $student = Convert-ResponseJson (Invoke-Api "$BaseUrl/api/students" POST @{
        studentCode = "AUTH-S-$suffix"; fullName = 'Auth Acceptance Student'; email = "auth-s-$suffix@example.test"
    } -Token $adminToken)

    $staffUser = Convert-ResponseJson (Invoke-Api "$BaseUrl/api/auth/users" POST @{
        email = "staff-$suffix@example.test"; password = $testPassword; role = 'STAFF'
    } -Token $adminToken)
    $createdUserIds.Add([long]$staffUser.userId)
    $teacherUser = Convert-ResponseJson (Invoke-Api "$BaseUrl/api/auth/users" POST @{
        email = "teacher-$suffix@example.test"; password = $testPassword; role = 'TEACHER'; teacherId = $teacher.teacherId
    } -Token $adminToken)
    $createdUserIds.Add([long]$teacherUser.userId)
    $studentUser = Convert-ResponseJson (Invoke-Api "$BaseUrl/api/auth/users" POST @{
        email = "student-$suffix@example.test"; password = $testPassword; role = 'STUDENT'; studentId = $student.studentId
    } -Token $adminToken)
    $createdUserIds.Add([long]$studentUser.userId)

    Invoke-ExpectedAuthProblem "$BaseUrl/api/auth/users" POST 403 @{
        email = "blocked-$suffix@example.test"; password = $testPassword; role = 'STAFF'
    } -Token ((Convert-ResponseJson (Invoke-Api "$BaseUrl/api/auth/login" POST @{
        email = $staffUser.email; password = $testPassword
    })).accessToken)

    $staffLogin = Convert-ResponseJson (Invoke-Api "$BaseUrl/api/auth/login" POST @{ email = $staffUser.email; password = $testPassword })
    $teacherLogin = Convert-ResponseJson (Invoke-Api "$BaseUrl/api/auth/login" POST @{ email = $teacherUser.email; password = $testPassword })
    $studentLogin = Convert-ResponseJson (Invoke-Api "$BaseUrl/api/auth/login" POST @{ email = $studentUser.email; password = $testPassword })

    Assert-True ((Invoke-Api "$BaseUrl/api/students?page=1&pageSize=1" GET -Token $staffLogin.accessToken).StatusCode -eq 200) 'STAFF could not read students.'
    Assert-True ((Invoke-Api "$BaseUrl/api/payments?page=1&pageSize=1" GET -Token $staffLogin.accessToken).StatusCode -eq 200) 'STAFF could not read payments.'
    Assert-True ((Invoke-Api "$BaseUrl/api/attendance?page=1&pageSize=1" GET -Token $teacherLogin.accessToken).StatusCode -eq 200) 'TEACHER could not read attendance.'
    Assert-True ((Invoke-Api "$BaseUrl/api/courses?page=1&pageSize=1" GET -Token $teacherLogin.accessToken).StatusCode -eq 200) 'TEACHER could not read courses.'
    Invoke-ExpectedAuthProblem "$BaseUrl/api/payments?page=1&pageSize=1" GET 403 -Token $teacherLogin.accessToken
    Invoke-ExpectedAuthProblem "$BaseUrl/api/students?page=1&pageSize=1" GET 403 -Token $teacherLogin.accessToken

    Assert-True ((Invoke-Api "$BaseUrl/api/courses?page=1&pageSize=1" GET -Token $studentLogin.accessToken).StatusCode -eq 200) 'STUDENT could not read courses.'
    Assert-True ((Invoke-Api "$BaseUrl/api/classes?page=1&pageSize=1" GET -Token $studentLogin.accessToken).StatusCode -eq 200) 'STUDENT could not read classes.'
    Invoke-ExpectedAuthProblem "$BaseUrl/api/attendance?page=1&pageSize=1" GET 403 -Token $studentLogin.accessToken
    Invoke-ExpectedAuthProblem "$BaseUrl/api/payments?page=1&pageSize=1" GET 403 -Token $studentLogin.accessToken

    $rotated = Convert-ResponseJson (Invoke-Api "$BaseUrl/api/auth/refresh" POST @{ refreshToken = $staffLogin.refreshToken })
    Assert-True ($rotated.refreshToken -ne $staffLogin.refreshToken) 'Refresh token was not rotated.'
    Invoke-ExpectedAuthProblem "$BaseUrl/api/auth/refresh" POST 401 @{ refreshToken = $staffLogin.refreshToken }
    Assert-True ((Invoke-Api "$BaseUrl/api/auth/logout" POST @{ refreshToken = $rotated.refreshToken } -Token $rotated.accessToken).StatusCode -eq 204) 'Logout did not return 204.'
    Invoke-ExpectedAuthProblem "$BaseUrl/api/auth/refresh" POST 401 @{ refreshToken = $rotated.refreshToken }

    $openApi = Convert-ResponseJson (Invoke-Api "$BaseUrl/openapi/v1.json" GET)
    Assert-True ($null -ne $openApi.paths.'/api/auth/login') 'OpenAPI is missing /api/auth/login.'
    Assert-True ($null -ne $openApi.paths.'/api/auth/refresh') 'OpenAPI is missing /api/auth/refresh.'

    [pscustomobject]@{
        Verification = 'PASS'; Unauthorized401 = 'PASS'; InvalidLogin401 = 'PASS'; AdminRole = 'PASS'
        StaffPolicy = 'PASS'; TeacherPolicy = 'PASS'; StudentPolicy = 'PASS'; Forbidden403 = 'PASS'
        RefreshRotation = 'PASS'; LogoutRevocation = 'PASS'; OpenApiAuthPaths = 'PASS'
    }
}
finally {
    if (-not [string]::IsNullOrWhiteSpace($adminToken)) {
        foreach ($userId in $createdUserIds) {
            try { Invoke-Api "$BaseUrl/api/auth/users/$userId" DELETE -Token $adminToken | Out-Null } catch { }
        }
        if ($null -ne $student) {
            try {
                Invoke-Api "$BaseUrl/api/students/$($student.studentId)" DELETE -Token $adminToken `
                    -Headers @{ 'If-Match' = '"' + $student.rowVersion + '"' } | Out-Null
            } catch { }
        }
        if ($null -ne $teacher) {
            try {
                Invoke-Api "$BaseUrl/api/teachers/$($teacher.teacherId)" DELETE -Token $adminToken `
                    -Headers @{ 'If-Match' = '"' + $teacher.rowVersion + '"' } | Out-Null
            } catch { }
        }
    }
}
