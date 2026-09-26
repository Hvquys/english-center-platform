[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^https?://[^\s/]+(?::\d+)?$')]
    [string]$BaseUrl = 'http://localhost:8080',

    [Parameter()]
    [string]$AccessToken
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$composeFile = Join-Path $repositoryRoot 'infrastructure\docker-compose.yml'
$environmentFile = Join-Path $repositoryRoot 'infrastructure\.env'
. (Join-Path $repositoryRoot 'scripts\api\auth-test-support.ps1')

function Assert-True {
    param([Parameter(Mandatory)][bool]$Condition, [Parameter(Mandatory)][string]$Message)
    if (-not $Condition) { throw $Message }
}

function Invoke-Redis {
    param([Parameter(Mandatory)][string]$Command)

    $arguments = @(
        'compose', '--env-file', $environmentFile, '-f', $composeFile,
        'exec', '-T', 'redis', 'sh', '-c',
        "REDISCLI_AUTH=`"`$REDIS_PASSWORD`" redis-cli --raw $Command"
    )
    $output = & docker @arguments
    if ($LASTEXITCODE -ne 0) { throw "Redis command failed with exit code $LASTEXITCODE." }
    $text = ($output | Out-String).Trim()
    if ($text -match '^(ERR|WRONGTYPE)') { throw "Redis command failed: $text" }
    return $text
}

function Invoke-ApiJson {
    param(
        [Parameter(Mandatory)][string]$Uri,
        [Parameter(Mandatory)][ValidateSet('GET', 'POST', 'PUT')][string]$Method,
        [Parameter()][object]$Body
    )

    $parameters = @{
        UseBasicParsing = $true
        Uri = $Uri
        Method = $Method
        Headers = Merge-AuthorizationHeaders
    }
    if ($PSBoundParameters.ContainsKey('Body')) {
        $parameters.Body = $Body | ConvertTo-Json -Depth 6
        $parameters.ContentType = 'application/json'
    }
    return (Invoke-WebRequest @parameters).Content | ConvertFrom-Json
}

if (-not (Test-Path -LiteralPath $environmentFile)) {
    throw 'Missing infrastructure/.env. Copy infrastructure/.env.example and provide local secret values.'
}

$health = $null
for ($attempt = 1; $attempt -le 12; $attempt++) {
    try {
        $health = Invoke-RestMethod -Uri "$BaseUrl/api/health" -Method GET
        if ($health.status -eq 'Healthy') { break }
    }
    catch {
        if ($attempt -eq 12) { throw }
    }
    Start-Sleep -Seconds 5
}
Assert-True ($health.status -eq 'Healthy') 'API health did not become Healthy.'
Assert-True (($health.checks | Where-Object name -eq 'redis').status -eq 'Healthy') 'Redis health check is not Healthy.'

if ([string]::IsNullOrWhiteSpace($AccessToken)) { $AccessToken = Get-AdminAuthToken $BaseUrl }
$suffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds().ToString()
$courseCode = "CACHE-$suffix"
$search = [uri]::EscapeDataString($courseCode)
$createdCourseId = $null
$cleanupComplete = $false

try {
$course = Invoke-ApiJson "$BaseUrl/api/courses" POST @{
    courseCode = $courseCode
    courseName = 'Redis Acceptance Course'
    levelCode = 'B1'
    description = 'TASK-016 cache acceptance'
    plannedHours = 36
    standardTuition = 1800000
}
$createdCourseId = $course.courseId

$detailKey = "english-center:v1:courses:detail:$($course.courseId)"
$detail = Invoke-ApiJson "$BaseUrl/api/courses/$($course.courseId)" GET
Assert-True ($detail.courseCode -eq $courseCode) 'Detail read did not return the created course.'
Assert-True ((Invoke-Redis "EXISTS '$detailKey'") -eq '1') 'Detail read did not populate Redis.'
$detailTtl = [int](Invoke-Redis "TTL '$detailKey'")
Assert-True ($detailTtl -gt 0 -and $detailTtl -le 300) 'Detail key TTL is outside 1..300 seconds.'

$list = Invoke-ApiJson "$BaseUrl/api/courses?page=1&pageSize=5&search=$search" GET
Assert-True ($list.totalCount -eq 1) 'Filtered course list did not return one record.'
$versionKey = 'english-center:v1:courses:list:version'
$firstVersion = (Invoke-Redis "HGET '$versionKey' data").Trim('"')
Assert-True (-not [string]::IsNullOrWhiteSpace($firstVersion)) 'Course-list cache version was not created.'
$firstListKey = Invoke-Redis "--scan --pattern 'english-center:v1:courses:list:${firstVersion}:*'"
Assert-True (-not [string]::IsNullOrWhiteSpace($firstListKey)) 'Course list read did not populate Redis.'
$listTtl = [int](Invoke-Redis "TTL '$firstListKey'")
Assert-True ($listTtl -gt 0 -and $listTtl -le 60) 'Course-list key TTL is outside 1..60 seconds.'

$updated = Invoke-ApiJson "$BaseUrl/api/courses/$($course.courseId)" PUT @{
    courseCode = $courseCode
    courseName = 'Redis Acceptance Course Updated'
    levelCode = 'B1'
    description = 'TASK-016 updated value'
    plannedHours = 40
    standardTuition = 1900000
    status = 'DRAFT'
    rowVersion = $course.rowVersion
}
Assert-True ((Invoke-Redis "EXISTS '$detailKey'") -eq '0') 'Update did not invalidate the detail key.'
$secondVersion = (Invoke-Redis "HGET '$versionKey' data").Trim('"')
Assert-True ($secondVersion -ne $firstVersion) 'Update did not rotate the course-list cache version.'

$updatedDetail = Invoke-ApiJson "$BaseUrl/api/courses/$($course.courseId)" GET
Assert-True ($updatedDetail.courseName -eq 'Redis Acceptance Course Updated') 'Read-through returned stale course data.'
Assert-True ((Invoke-Redis "EXISTS '$detailKey'") -eq '1') 'Updated detail was not cached again.'
$updatedList = Invoke-ApiJson "$BaseUrl/api/courses?page=1&pageSize=5&search=$search" GET
Assert-True ($updatedList.items[0].courseName -eq 'Redis Acceptance Course Updated') 'New list version returned stale course data.'

$headers = Merge-AuthorizationHeaders @{ 'If-Match' = '"' + $updated.rowVersion + '"' }
$deleteResponse = Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/api/courses/$($course.courseId)" -Method DELETE -Headers $headers
Assert-True ($deleteResponse.StatusCode -eq 204) 'Course cleanup did not return HTTP 204.'
$cleanupComplete = $true
Assert-True ((Invoke-Redis "EXISTS '$detailKey'") -eq '0') 'Delete did not invalidate the detail key.'
$thirdVersion = (Invoke-Redis "HGET '$versionKey' data").Trim('"')
Assert-True ($thirdVersion -ne $secondVersion) 'Delete did not rotate the course-list cache version.'

try {
    Invoke-ApiJson "$BaseUrl/api/courses/$($course.courseId)" GET | Out-Null
    throw 'Soft-deleted course was still available.'
}
catch [Microsoft.PowerShell.Commands.HttpResponseException] {
    Assert-True ([int]$_.Exception.Response.StatusCode -eq 404) 'Deleted course did not return HTTP 404.'
}
$deletedList = Invoke-ApiJson "$BaseUrl/api/courses?page=1&pageSize=5&search=$search" GET
Assert-True ($deletedList.totalCount -eq 0) 'Deleted course remained in the current list cache.'

[pscustomobject]@{
    Verification = 'PASS'
    RedisHealth = 'PASS'
    DetailReadThrough = 'PASS'
    DetailTtl = 'PASS'
    ListReadThrough = 'PASS'
    ListTtl = 'PASS'
    UpdateInvalidation = 'PASS'
    DeleteInvalidation = 'PASS'
    SoftDeleteCleanup = 'PASS'
}
}
finally {
    if ($null -ne $createdCourseId -and -not $cleanupComplete) {
        try {
            $current = Invoke-ApiJson "$BaseUrl/api/courses/$createdCourseId" GET
            $cleanupHeaders = Merge-AuthorizationHeaders @{ 'If-Match' = '"' + $current.rowVersion + '"' }
            Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/api/courses/$createdCourseId" -Method DELETE -Headers $cleanupHeaders | Out-Null
        }
        catch {
            Write-Warning "Could not remove TASK-016 test course $createdCourseId after a failed check."
        }
    }
}
