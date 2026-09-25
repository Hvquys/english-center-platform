[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^https?://[^\s/]+(?::\d+)?$')]
    [string]$BaseUrl = 'http://localhost:8080'
)

$ErrorActionPreference = 'Stop'

function Invoke-JsonRequest {
    param(
        [Parameter(Mandatory)]
        [string]$Uri,

        [Parameter(Mandatory)]
        [ValidateSet('GET', 'POST', 'PUT')]
        [string]$Method,

        [Parameter()]
        [object]$Body
    )

    $parameters = @{
        UseBasicParsing = $true
        Uri = $Uri
        Method = $Method
    }

    if ($PSBoundParameters.ContainsKey('Body')) {
        $parameters.Body = $Body | ConvertTo-Json -Depth 8
        $parameters.ContentType = 'application/json'
    }

    $response = Invoke-WebRequest @parameters
    return $response.Content | ConvertFrom-Json
}

function Invoke-ExpectedProblem {
    param(
        [Parameter(Mandatory)]
        [string]$Uri,

        [Parameter(Mandatory)]
        [ValidateSet('GET', 'POST', 'PUT', 'DELETE')]
        [string]$Method,

        [Parameter(Mandatory)]
        [int]$ExpectedStatus,

        [Parameter()]
        [object]$Body,

        [Parameter()]
        [hashtable]$Headers
    )

    try {
        $parameters = @{
            UseBasicParsing = $true
            Uri = $Uri
            Method = $Method
        }

        if ($PSBoundParameters.ContainsKey('Body')) {
            $parameters.Body = $Body | ConvertTo-Json -Depth 8
            $parameters.ContentType = 'application/json'
        }

        if ($null -ne $Headers) {
            $parameters.Headers = $Headers
        }

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
    param(
        [Parameter(Mandatory)]
        [bool]$Condition,

        [Parameter(Mandatory)]
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

$suffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds().ToString()
$studentCode = "S-$suffix"
$teacherCode = "T-$suffix"

$studentCreate = @{
    studentCode = $studentCode
    fullName = 'Acceptance Student'
    dateOfBirth = '2001-02-03'
    gender = 'OTHER'
    email = "$($studentCode.ToLowerInvariant())@example.test"
    phone = '0900000001'
    guardianName = 'Acceptance Guardian'
    guardianPhone = '0900000002'
}
$student = Invoke-JsonRequest -Uri "$BaseUrl/api/students" -Method POST -Body $studentCreate
Assert-True ($student.studentId -gt 0) 'Student POST did not return an ID.'
Assert-True (-not [string]::IsNullOrWhiteSpace($student.rowVersion)) 'Student POST did not return rowVersion.'

$studentList = Invoke-JsonRequest -Uri "$BaseUrl/api/students?page=1&pageSize=5&search=$studentCode" -Method GET
Assert-True ($studentList.totalCount -eq 1) 'Student search did not return exactly one active record.'
Assert-True ($studentList.items[0].studentId -eq $student.studentId) 'Student search returned the wrong record.'
Invoke-ExpectedProblem -Uri "$BaseUrl/api/students?page=1&pageSize=101" -Method GET -ExpectedStatus 400 | Out-Null

$studentById = Invoke-JsonRequest -Uri "$BaseUrl/api/students/$($student.studentId)" -Method GET
Assert-True ($studentById.studentCode -eq $studentCode) 'Student GET returned the wrong code.'

$studentUpdate = @{
    studentCode = $studentCode
    fullName = 'Acceptance Student Updated'
    dateOfBirth = '2001-02-03'
    gender = 'OTHER'
    email = "$($studentCode.ToLowerInvariant())@example.test"
    phone = '0900000003'
    guardianName = 'Acceptance Guardian'
    guardianPhone = '0900000004'
    status = 'ACTIVE'
    rowVersion = $student.rowVersion
}
$updatedStudent = Invoke-JsonRequest -Uri "$BaseUrl/api/students/$($student.studentId)" -Method PUT -Body $studentUpdate
Assert-True ($updatedStudent.fullName -eq 'Acceptance Student Updated') 'Student PUT did not persist the new name.'
Assert-True ($updatedStudent.rowVersion -ne $student.rowVersion) 'Student PUT did not change rowVersion.'

$staleStudentUpdate = $studentUpdate.Clone()
$staleStudentUpdate.fullName = 'Stale Student Update'
Invoke-ExpectedProblem -Uri "$BaseUrl/api/students/$($student.studentId)" -Method PUT -ExpectedStatus 409 -Body $staleStudentUpdate | Out-Null

Invoke-ExpectedProblem -Uri "$BaseUrl/api/students/$($student.studentId)" -Method DELETE -ExpectedStatus 400 | Out-Null
$studentDeleteHeaders = @{ 'If-Match' = '"' + $updatedStudent.rowVersion + '"' }
$studentDelete = Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/api/students/$($student.studentId)" -Method DELETE -Headers $studentDeleteHeaders
Assert-True ($studentDelete.StatusCode -eq 204) 'Student DELETE did not return 204.'
Invoke-ExpectedProblem -Uri "$BaseUrl/api/students/$($student.studentId)" -Method GET -ExpectedStatus 404 | Out-Null
Invoke-ExpectedProblem -Uri "$BaseUrl/api/students" -Method POST -ExpectedStatus 409 -Body $studentCreate | Out-Null

$teacherCreate = @{
    teacherCode = $teacherCode
    fullName = 'Acceptance Teacher'
    email = "$($teacherCode.ToLowerInvariant())@example.test"
    phone = '0910000001'
    specialization = 'English Communication'
}
$teacher = Invoke-JsonRequest -Uri "$BaseUrl/api/teachers" -Method POST -Body $teacherCreate
Assert-True ($teacher.teacherId -gt 0) 'Teacher POST did not return an ID.'
Assert-True (-not [string]::IsNullOrWhiteSpace($teacher.rowVersion)) 'Teacher POST did not return rowVersion.'

$teacherList = Invoke-JsonRequest -Uri "$BaseUrl/api/teachers?page=1&pageSize=5&search=$teacherCode" -Method GET
Assert-True ($teacherList.totalCount -eq 1) 'Teacher search did not return exactly one active record.'
Assert-True ($teacherList.items[0].teacherId -eq $teacher.teacherId) 'Teacher search returned the wrong record.'

$teacherUpdate = @{
    teacherCode = $teacherCode
    fullName = 'Acceptance Teacher Updated'
    email = "$($teacherCode.ToLowerInvariant())@example.test"
    phone = '0910000002'
    specialization = 'IELTS'
    status = 'ON_LEAVE'
    rowVersion = $teacher.rowVersion
}
$updatedTeacher = Invoke-JsonRequest -Uri "$BaseUrl/api/teachers/$($teacher.teacherId)" -Method PUT -Body $teacherUpdate
Assert-True ($updatedTeacher.status -eq 'ON_LEAVE') 'Teacher PUT did not persist the status.'
Assert-True ($updatedTeacher.rowVersion -ne $teacher.rowVersion) 'Teacher PUT did not change rowVersion.'

$filteredTeachers = Invoke-JsonRequest -Uri "$BaseUrl/api/teachers?page=1&pageSize=5&search=$teacherCode&status=ON_LEAVE" -Method GET
Assert-True ($filteredTeachers.totalCount -eq 1) 'Teacher status filter did not return the updated record.'

$teacherDeleteHeaders = @{ 'If-Match' = '"' + $updatedTeacher.rowVersion + '"' }
$teacherDelete = Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/api/teachers/$($teacher.teacherId)" -Method DELETE -Headers $teacherDeleteHeaders
Assert-True ($teacherDelete.StatusCode -eq 204) 'Teacher DELETE did not return 204.'
Invoke-ExpectedProblem -Uri "$BaseUrl/api/teachers/$($teacher.teacherId)" -Method GET -ExpectedStatus 404 | Out-Null
Invoke-ExpectedProblem -Uri "$BaseUrl/api/teachers" -Method POST -ExpectedStatus 409 -Body $teacherCreate | Out-Null

$openApi = Invoke-JsonRequest -Uri "$BaseUrl/openapi/v1.json" -Method GET
Assert-True ($null -ne $openApi.paths.'/api/students'.get) 'OpenAPI is missing GET /api/students.'
Assert-True ($null -ne $openApi.paths.'/api/students'.post) 'OpenAPI is missing POST /api/students.'
Assert-True ($null -ne $openApi.paths.'/api/students/{studentId}'.put) 'OpenAPI is missing PUT /api/students/{studentId}.'
Assert-True ($null -ne $openApi.paths.'/api/students/{studentId}'.delete) 'OpenAPI is missing DELETE /api/students/{studentId}.'
Assert-True ($null -ne $openApi.paths.'/api/teachers'.get) 'OpenAPI is missing GET /api/teachers.'
Assert-True ($null -ne $openApi.paths.'/api/teachers'.post) 'OpenAPI is missing POST /api/teachers.'
Assert-True ($null -ne $openApi.paths.'/api/teachers/{teacherId}'.put) 'OpenAPI is missing PUT /api/teachers/{teacherId}.'
Assert-True ($null -ne $openApi.paths.'/api/teachers/{teacherId}'.delete) 'OpenAPI is missing DELETE /api/teachers/{teacherId}.'

[pscustomobject]@{
    Verification = 'PASS'
    StudentCreate = 'PASS'
    StudentSearchAndPaging = 'PASS'
    StudentUpdate = 'PASS'
    StudentStaleWrite409 = 'PASS'
    StudentSoftDelete = 'PASS'
    TeacherCreate = 'PASS'
    TeacherSearchStatusFilter = 'PASS'
    TeacherUpdate = 'PASS'
    TeacherSoftDelete = 'PASS'
    OpenApiPeopleCrud = 'PASS'
}
