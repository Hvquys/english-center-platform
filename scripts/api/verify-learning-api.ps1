[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^https?://[^\s/]+(?::\d+)?$')]
    [string]$BaseUrl = 'http://localhost:8080'
)

$ErrorActionPreference = 'Stop'

function Invoke-JsonRequest {
    param(
        [Parameter(Mandatory)][string]$Uri,
        [Parameter(Mandatory)][ValidateSet('GET', 'POST', 'PUT')][string]$Method,
        [Parameter()][object]$Body
    )
    $parameters = @{ UseBasicParsing = $true; Uri = $Uri; Method = $Method }
    if ($PSBoundParameters.ContainsKey('Body')) {
        $parameters.Body = $Body | ConvertTo-Json -Depth 8
        $parameters.ContentType = 'application/json'
    }
    $response = Invoke-WebRequest @parameters
    return $response.Content | ConvertFrom-Json
}

function Invoke-ExpectedProblem {
    param(
        [Parameter(Mandatory)][string]$Uri,
        [Parameter(Mandatory)][ValidateSet('GET', 'POST', 'PUT', 'DELETE')][string]$Method,
        [Parameter(Mandatory)][int]$ExpectedStatus,
        [Parameter()][object]$Body,
        [Parameter()][hashtable]$Headers
    )
    try {
        $parameters = @{ UseBasicParsing = $true; Uri = $Uri; Method = $Method }
        if ($PSBoundParameters.ContainsKey('Body')) {
            $parameters.Body = $Body | ConvertTo-Json -Depth 8
            $parameters.ContentType = 'application/json'
        }
        if ($null -ne $Headers) { $parameters.Headers = $Headers }
        Invoke-WebRequest @parameters | Out-Null
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
        return $problem
    }
}

function Assert-True {
    param([Parameter(Mandatory)][bool]$Condition, [Parameter(Mandatory)][string]$Message)
    if (-not $Condition) { throw $Message }
}

function Remove-VersionedResource {
    param([Parameter(Mandatory)][string]$Uri, [Parameter(Mandatory)][string]$RowVersion)
    $headers = @{ 'If-Match' = '"' + $RowVersion + '"' }
    $response = Invoke-WebRequest -UseBasicParsing -Uri $Uri -Method DELETE -Headers $headers
    Assert-True ($response.StatusCode -eq 204) "DELETE $Uri did not return 204."
}

$suffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds().ToString()
$courseCode = "C-$suffix"
$classCode = "CL-$suffix"
$teacherCode = "T9-$suffix"
$studentCode1 = "S9A-$suffix"
$studentCode2 = "S9B-$suffix"

$courseCreate = @{
    courseCode = $courseCode; courseName = 'Acceptance Course'; levelCode = 'B1'
    description = 'TASK-009 acceptance'; plannedHours = 40; standardTuition = 2500000
}
$course = Invoke-JsonRequest "$BaseUrl/api/courses" POST $courseCreate
Assert-True ($course.status -eq 'DRAFT') 'Course did not start in DRAFT.'
$courseUpdate = @{
    courseCode = $courseCode; courseName = 'Acceptance Course Updated'; levelCode = 'B1'
    description = 'Ready for classes'; plannedHours = 42; standardTuition = 2500000
    status = 'ACTIVE'; rowVersion = $course.rowVersion
}
$activeCourse = Invoke-JsonRequest "$BaseUrl/api/courses/$($course.courseId)" PUT $courseUpdate
Assert-True ($activeCourse.status -eq 'ACTIVE') 'Course did not become ACTIVE.'

$teacher = Invoke-JsonRequest "$BaseUrl/api/teachers" POST @{
    teacherCode = $teacherCode; fullName = 'TASK009 Teacher'; email = "$($teacherCode.ToLowerInvariant())@example.test"; specialization = 'B1'
}
$student1 = Invoke-JsonRequest "$BaseUrl/api/students" POST @{
    studentCode = $studentCode1; fullName = 'TASK009 Student One'; email = "$($studentCode1.ToLowerInvariant())@example.test"
}
$student2 = Invoke-JsonRequest "$BaseUrl/api/students" POST @{
    studentCode = $studentCode2; fullName = 'TASK009 Student Two'; email = "$($studentCode2.ToLowerInvariant())@example.test"
}

$classCreate = @{
    classCode = $classCode; courseId = $activeCourse.courseId; teacherId = $teacher.teacherId
    className = 'Acceptance B1 Class'; startDate = '2026-10-01'; endDate = '2026-12-15'
    capacity = 1; scheduleNote = 'Monday and Wednesday'; roomName = 'R-101'
}
$class = Invoke-JsonRequest "$BaseUrl/api/classes" POST $classCreate
Assert-True ($class.status -eq 'PLANNED') 'Class did not start in PLANNED.'
$classUpdate = $classCreate.Clone()
$classUpdate.status = 'OPEN'
$classUpdate.rowVersion = $class.rowVersion
$openClass = Invoke-JsonRequest "$BaseUrl/api/classes/$($class.classId)" PUT $classUpdate
Assert-True ($openClass.status -eq 'OPEN') 'Class did not become OPEN.'

$enrollment = Invoke-JsonRequest "$BaseUrl/api/enrollments" POST @{
    studentId = $student1.studentId; classId = $openClass.classId
}
Assert-True ($enrollment.status -eq 'PENDING') 'Enrollment did not start in PENDING.'
Assert-True ($enrollment.agreedTuition -eq 2500000) 'Enrollment did not default to course tuition.'

Invoke-ExpectedProblem "$BaseUrl/api/enrollments" POST 409 @{
    studentId = $student1.studentId; classId = $openClass.classId
} | Out-Null
Invoke-ExpectedProblem "$BaseUrl/api/enrollments" POST 409 @{
    studentId = $student2.studentId; classId = $openClass.classId
} | Out-Null

$activeEnrollment = Invoke-JsonRequest "$BaseUrl/api/enrollments/$($enrollment.enrollmentId)" PUT @{
    agreedTuition = 2400000; status = 'ACTIVE'; completionNote = $null; rowVersion = $enrollment.rowVersion
}
Assert-True ($activeEnrollment.status -eq 'ACTIVE') 'Enrollment did not become ACTIVE.'
Invoke-ExpectedProblem -Uri "$BaseUrl/api/enrollments/$($enrollment.enrollmentId)" -Method DELETE -ExpectedStatus 409 `
    -Headers @{ 'If-Match' = '"' + $activeEnrollment.rowVersion + '"' } | Out-Null
Invoke-ExpectedProblem -Uri "$BaseUrl/api/classes/$($openClass.classId)" -Method DELETE -ExpectedStatus 409 `
    -Headers @{ 'If-Match' = '"' + $openClass.rowVersion + '"' } | Out-Null
Invoke-ExpectedProblem -Uri "$BaseUrl/api/courses/$($activeCourse.courseId)" -Method DELETE -ExpectedStatus 409 `
    -Headers @{ 'If-Match' = '"' + $activeCourse.rowVersion + '"' } | Out-Null

$archiveCourse = $courseUpdate.Clone()
$archiveCourse.status = 'ARCHIVED'
$archiveCourse.rowVersion = $activeCourse.rowVersion
Invoke-ExpectedProblem "$BaseUrl/api/courses/$($activeCourse.courseId)" PUT 409 $archiveCourse | Out-Null
Invoke-ExpectedProblem "$BaseUrl/api/enrollments/$($enrollment.enrollmentId)" PUT 409 @{
    agreedTuition = 2400000; status = 'PENDING'; completionNote = $null; rowVersion = $activeEnrollment.rowVersion
} | Out-Null

$invalidClassUpdate = $classCreate.Clone()
$invalidClassUpdate.status = 'PLANNED'
$invalidClassUpdate.rowVersion = $openClass.rowVersion
Invoke-ExpectedProblem "$BaseUrl/api/classes/$($openClass.classId)" PUT 409 $invalidClassUpdate | Out-Null

$courseList = Invoke-JsonRequest "$BaseUrl/api/courses?page=1&pageSize=5&search=$courseCode&status=ACTIVE" GET
$classList = Invoke-JsonRequest "$BaseUrl/api/classes?page=1&pageSize=5&search=$classCode&status=OPEN" GET
$enrollmentList = Invoke-JsonRequest "$BaseUrl/api/enrollments?page=1&pageSize=5&studentId=$($student1.studentId)&classId=$($openClass.classId)&status=ACTIVE" GET
Assert-True ($courseList.totalCount -eq 1) 'Course filter did not return one record.'
Assert-True ($classList.totalCount -eq 1) 'Class filter did not return one record.'
Assert-True ($enrollmentList.totalCount -eq 1) 'Enrollment filter did not return one record.'

$cancelledEnrollment = Invoke-JsonRequest "$BaseUrl/api/enrollments/$($enrollment.enrollmentId)" PUT @{
    agreedTuition = 2400000; status = 'CANCELLED'; completionNote = 'Acceptance cleanup'; rowVersion = $activeEnrollment.rowVersion
}
Remove-VersionedResource "$BaseUrl/api/enrollments/$($enrollment.enrollmentId)" $cancelledEnrollment.rowVersion

$cancelClass = $classCreate.Clone()
$cancelClass.status = 'CANCELLED'
$cancelClass.rowVersion = $openClass.rowVersion
$cancelledClass = Invoke-JsonRequest "$BaseUrl/api/classes/$($openClass.classId)" PUT $cancelClass
Remove-VersionedResource "$BaseUrl/api/classes/$($openClass.classId)" $cancelledClass.rowVersion
Remove-VersionedResource "$BaseUrl/api/courses/$($activeCourse.courseId)" $activeCourse.rowVersion
Remove-VersionedResource "$BaseUrl/api/teachers/$($teacher.teacherId)" $teacher.rowVersion
Remove-VersionedResource "$BaseUrl/api/students/$($student1.studentId)" $student1.rowVersion
Remove-VersionedResource "$BaseUrl/api/students/$($student2.studentId)" $student2.rowVersion

$openApi = Invoke-JsonRequest "$BaseUrl/openapi/v1.json" GET
foreach ($path in @('/api/courses', '/api/courses/{courseId}', '/api/classes', '/api/classes/{classId}', '/api/enrollments', '/api/enrollments/{enrollmentId}')) {
    Assert-True ($null -ne $openApi.paths.$path) "OpenAPI is missing $path."
}

[pscustomobject]@{
    Verification = 'PASS'
    CourseCrud = 'PASS'
    ClassLifecycle = 'PASS'
    EnrollmentLifecycle = 'PASS'
    DuplicateEnrollment409 = 'PASS'
    Capacity409 = 'PASS'
    ActiveRelationshipDelete409 = 'PASS'
    CourseArchiveGuard409 = 'PASS'
    InvalidTransitions409 = 'PASS'
    SearchAndFilters = 'PASS'
    SoftDeleteCleanup = 'PASS'
    OpenApiLearningPaths = 'PASS'
}
