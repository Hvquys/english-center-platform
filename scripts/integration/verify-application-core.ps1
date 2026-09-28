[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^https?://[^\s/]+(?::\d+)?$')]
    [string]$WebBaseUrl = 'http://localhost:5173',

    [Parameter()]
    [string]$AccessToken
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\api\auth-test-support.ps1')

function Assert-True {
    param([Parameter(Mandatory)][bool]$Condition, [Parameter(Mandatory)][string]$Message)
    if (-not $Condition) { throw $Message }
}

function Invoke-CoreRequest {
    param(
        [Parameter(Mandatory)][string]$Uri,
        [Parameter(Mandatory)][ValidateSet('GET', 'POST', 'PUT', 'DELETE')][string]$Method,
        [Parameter()][object]$Body,
        [Parameter()][string]$Token,
        [Parameter()][hashtable]$Headers
    )

    $request = @{ UseBasicParsing = $true; Uri = $Uri; Method = $Method }
    $requestHeaders = @{}
    if (-not [string]::IsNullOrWhiteSpace($Token)) { $requestHeaders.Authorization = "Bearer $Token" }
    if ($null -ne $Headers) {
        foreach ($key in $Headers.Keys) { $requestHeaders[$key] = $Headers[$key] }
    }
    if ($requestHeaders.Count -gt 0) { $request.Headers = $requestHeaders }
    if ($PSBoundParameters.ContainsKey('Body')) {
        $request.Body = $Body | ConvertTo-Json -Depth 8
        $request.ContentType = 'application/json'
    }
    return Invoke-WebRequest @request
}

function Convert-CoreJson {
    param([Parameter(Mandatory)]$Response)
    return $Response.Content | ConvertFrom-Json
}

function Invoke-CoreJson {
    param(
        [Parameter(Mandatory)][string]$Uri,
        [Parameter(Mandatory)][ValidateSet('GET', 'POST', 'PUT')][string]$Method,
        [Parameter()][object]$Body,
        [Parameter()][string]$Token
    )

    $request = @{ Uri = $Uri; Method = $Method; Token = $Token }
    if ($PSBoundParameters.ContainsKey('Body')) { $request.Body = $Body }
    return Convert-CoreJson (Invoke-CoreRequest @request)
}

function Invoke-ExpectedCoreProblem {
    param(
        [Parameter(Mandatory)][string]$Uri,
        [Parameter(Mandatory)][ValidateSet('GET', 'POST', 'PUT', 'DELETE')][string]$Method,
        [Parameter(Mandatory)][int]$ExpectedStatus,
        [Parameter()][object]$Body,
        [Parameter()][string]$Token,
        [Parameter()][hashtable]$Headers
    )

    try {
        $request = @{ Uri = $Uri; Method = $Method }
        if ($PSBoundParameters.ContainsKey('Body')) { $request.Body = $Body }
        if (-not [string]::IsNullOrWhiteSpace($Token)) { $request.Token = $Token }
        if ($null -ne $Headers) { $request.Headers = $Headers }
        Invoke-CoreRequest @request | Out-Null
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

function Remove-CoreResource {
    param(
        [Parameter(Mandatory)][string]$Uri,
        [Parameter(Mandatory)][string]$RowVersion,
        [Parameter(Mandatory)][string]$Token
    )
    $response = Invoke-CoreRequest $Uri DELETE -Token $Token -Headers @{
        'If-Match' = '"' + $RowVersion + '"'
    }
    Assert-True ($response.StatusCode -eq 204) "DELETE $Uri did not return 204."
}

function Get-CoreResourceOrNull {
    param([Parameter(Mandatory)][string]$Uri, [Parameter(Mandatory)][string]$Token)
    try { return Invoke-CoreJson $Uri GET -Token $Token }
    catch [Microsoft.PowerShell.Commands.HttpResponseException] {
        if ([int]$_.Exception.Response.StatusCode -eq 404) { return $null }
        throw
    }
}

function Invoke-BestEffortCleanup {
    param([Parameter(Mandatory)][scriptblock]$Action, [Parameter(Mandatory)][string]$Description)
    try { & $Action }
    catch { Write-Warning "Cleanup could not complete '$Description': $($_.Exception.Message)" }
}

$apiBaseUrl = "$WebBaseUrl/api"
if ([string]::IsNullOrWhiteSpace($AccessToken)) { $AccessToken = Get-AdminAuthToken $WebBaseUrl }
$adminToken = $AccessToken
$suffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds().ToString()
$today = [DateTime]::UtcNow.Date
$attendanceDate = $today.ToString('yyyy-MM-dd')
$startDate = $today.AddDays(-7).ToString('yyyy-MM-dd')
$endDate = $today.AddDays(30).ToString('yyyy-MM-dd')
$futureDate = $today.AddDays(31).ToString('yyyy-MM-dd')
$courseCode = "CORE-C-$suffix"
$classCode = "CORE-CL-$suffix"
$teacherCode = "CORE-T-$suffix"
$studentCode = "CORE-S-$suffix"
$paymentReference = "CORE-PAY-$suffix"
$testPassword = [Convert]::ToBase64String(
    [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(24)) + 'Aa1!'

$course = $null
$teacher = $null
$student = $null
$class = $null
$enrollment = $null
$attendance = $null
$payment = $null
$overpayment = $null
$createdUserIds = [System.Collections.Generic.List[long]]::new()
$verificationSucceeded = $false

try {
    foreach ($route in @('students', 'classes', 'enrollments', 'attendance', 'payments')) {
        $page = Invoke-CoreRequest "$WebBaseUrl/$route" GET
        Assert-True ($page.StatusCode -eq 200) "SPA route /$route did not return 200."
        Assert-True ($page.Content -match '<div id="root"></div>') "SPA route /$route did not return the React application shell."
    }

    $adminMe = Invoke-CoreJson "$apiBaseUrl/auth/me" GET -Token $adminToken
    Assert-True ($adminMe.role -eq 'ADMIN') 'The verification token does not belong to an ADMIN user.'

    $course = Invoke-CoreJson "$apiBaseUrl/courses" POST -Token $adminToken -Body @{
        courseCode = $courseCode; courseName = 'Application Core Verification'; levelCode = 'B2'
        description = 'TASK-012 isolated end-to-end fixture'; plannedHours = 48; standardTuition = 2400000
    }
    Assert-True ($course.status -eq 'DRAFT') 'Course did not start in DRAFT.'
    $course = Invoke-CoreJson "$apiBaseUrl/courses/$($course.courseId)" PUT -Token $adminToken -Body @{
        courseCode = $courseCode; courseName = 'Application Core Verification'; levelCode = 'B2'
        description = 'TASK-012 isolated end-to-end fixture'; plannedHours = 48; standardTuition = 2400000
        status = 'ACTIVE'; rowVersion = $course.rowVersion
    }

    $teacher = Invoke-CoreJson "$apiBaseUrl/teachers" POST -Token $adminToken -Body @{
        teacherCode = $teacherCode; fullName = 'Application Core Teacher'
        email = "$($teacherCode.ToLowerInvariant())@example.test"; specialization = 'B2'
    }
    $student = Invoke-CoreJson "$apiBaseUrl/students" POST -Token $adminToken -Body @{
        studentCode = $studentCode; fullName = 'Application Core Student'
        email = "$($studentCode.ToLowerInvariant())@example.test"; guardianName = 'Core Guardian'
    }

    $classRequest = @{
        classCode = $classCode; courseId = $course.courseId; teacherId = $teacher.teacherId
        className = 'Application Core B2 Class'; startDate = $startDate; endDate = $endDate
        capacity = 12; scheduleNote = 'TASK-012 verification schedule'; roomName = 'CORE-ROOM'
    }
    $class = Invoke-CoreJson "$apiBaseUrl/classes" POST -Token $adminToken -Body $classRequest
    Assert-True ($class.status -eq 'PLANNED') 'Class did not start in PLANNED.'
    $openClassRequest = $classRequest.Clone()
    $openClassRequest.status = 'OPEN'
    $openClassRequest.rowVersion = $class.rowVersion
    $class = Invoke-CoreJson "$apiBaseUrl/classes/$($class.classId)" PUT -Token $adminToken -Body $openClassRequest

    $enrollment = Invoke-CoreJson "$apiBaseUrl/enrollments" POST -Token $adminToken -Body @{
        studentId = $student.studentId; classId = $class.classId
    }
    Assert-True ($enrollment.status -eq 'PENDING') 'Enrollment did not start in PENDING.'
    Assert-True ($enrollment.agreedTuition -eq 2400000) 'Enrollment did not inherit the course tuition.'
    $enrollment = Invoke-CoreJson "$apiBaseUrl/enrollments/$($enrollment.enrollmentId)" PUT -Token $adminToken -Body @{
        agreedTuition = 2400000; status = 'ACTIVE'; completionNote = $null; rowVersion = $enrollment.rowVersion
    }

    $progressClassRequest = $classRequest.Clone()
    $progressClassRequest.status = 'IN_PROGRESS'
    $progressClassRequest.rowVersion = $class.rowVersion
    $class = Invoke-CoreJson "$apiBaseUrl/classes/$($class.classId)" PUT -Token $adminToken -Body $progressClassRequest

    Invoke-ExpectedCoreProblem "$apiBaseUrl/attendance" POST 400 -Token $adminToken -Body @{
        enrollmentId = $enrollment.enrollmentId; attendanceDate = $futureDate
        status = 'PRESENT'; checkInAt = "$($futureDate)T08:00:00Z"; recordedBy = 'task-012'
    } | Out-Null

    $attendance = Invoke-CoreJson "$apiBaseUrl/attendance" POST -Token $adminToken -Body @{
        enrollmentId = $enrollment.enrollmentId; attendanceDate = $attendanceDate
        status = 'PRESENT'; checkInAt = "$($attendanceDate)T08:00:00Z"
        note = 'Created through frontend proxy'; recordedBy = 'task-012'
    }
    Invoke-ExpectedCoreProblem "$apiBaseUrl/attendance" POST 409 -Token $adminToken -Body @{
        enrollmentId = $enrollment.enrollmentId; attendanceDate = $attendanceDate
        status = 'LATE'; checkInAt = "$($attendanceDate)T08:15:00Z"; recordedBy = 'task-012'
    } | Out-Null

    $teacherEmail = "core-teacher-$suffix@example.test"
    $studentEmail = "core-student-$suffix@example.test"
    $teacherUser = Invoke-CoreJson "$apiBaseUrl/auth/users" POST -Token $adminToken -Body @{
        email = $teacherEmail; password = $testPassword; role = 'TEACHER'; teacherId = $teacher.teacherId
    }
    $createdUserIds.Add([long]$teacherUser.userId)
    $studentUser = Invoke-CoreJson "$apiBaseUrl/auth/users" POST -Token $adminToken -Body @{
        email = $studentEmail; password = $testPassword; role = 'STUDENT'; studentId = $student.studentId
    }
    $createdUserIds.Add([long]$studentUser.userId)

    $teacherLogin = Invoke-CoreJson "$apiBaseUrl/auth/login" POST -Token '' -Body @{
        email = $teacherEmail; password = $testPassword
    }
    $studentLogin = Invoke-CoreJson "$apiBaseUrl/auth/login" POST -Token '' -Body @{
        email = $studentEmail; password = $testPassword
    }

    Assert-True ((Invoke-CoreRequest "$apiBaseUrl/classes?page=1&pageSize=1" GET -Token $teacherLogin.accessToken).StatusCode -eq 200) 'TEACHER could not read classes.'
    Assert-True ((Invoke-CoreRequest "$apiBaseUrl/attendance?page=1&pageSize=1" GET -Token $teacherLogin.accessToken).StatusCode -eq 200) 'TEACHER could not read attendance.'
    Invoke-ExpectedCoreProblem "$apiBaseUrl/students?page=1&pageSize=1" GET 403 -Token $teacherLogin.accessToken | Out-Null
    Invoke-ExpectedCoreProblem "$apiBaseUrl/enrollments?page=1&pageSize=1" GET 403 -Token $teacherLogin.accessToken | Out-Null
    Invoke-ExpectedCoreProblem "$apiBaseUrl/payments?page=1&pageSize=1" GET 403 -Token $teacherLogin.accessToken | Out-Null

    Assert-True ((Invoke-CoreRequest "$apiBaseUrl/classes?page=1&pageSize=1" GET -Token $studentLogin.accessToken).StatusCode -eq 200) 'STUDENT could not read classes.'
    Invoke-ExpectedCoreProblem "$apiBaseUrl/students?page=1&pageSize=1" GET 403 -Token $studentLogin.accessToken | Out-Null
    Invoke-ExpectedCoreProblem "$apiBaseUrl/attendance?page=1&pageSize=1" GET 403 -Token $studentLogin.accessToken | Out-Null
    Invoke-ExpectedCoreProblem "$apiBaseUrl/payments?page=1&pageSize=1" GET 403 -Token $studentLogin.accessToken | Out-Null

    $originalAttendanceVersion = $attendance.rowVersion
    $attendance = Invoke-CoreJson "$apiBaseUrl/attendance/$($attendance.attendanceId)" PUT -Token $teacherLogin.accessToken -Body @{
        status = 'LATE'; checkInAt = "$($attendanceDate)T08:15:00Z"
        note = 'Corrected by linked teacher'; recordedBy = $teacherCode; rowVersion = $originalAttendanceVersion
    }
    Assert-True ($attendance.status -eq 'LATE') 'TEACHER attendance correction did not persist.'
    Invoke-ExpectedCoreProblem "$apiBaseUrl/attendance/$($attendance.attendanceId)" PUT 409 -Token $adminToken -Body @{
        status = 'ABSENT'; checkInAt = $null; note = 'Intentional stale update'
        recordedBy = 'task-012'; rowVersion = $originalAttendanceVersion
    } | Out-Null

    $payment = Invoke-CoreJson "$apiBaseUrl/payments" POST -Token $adminToken -Body @{
        enrollmentId = $enrollment.enrollmentId; paymentDate = $attendanceDate; amount = 1500000
        paymentMethod = 'BANK_TRANSFER'; paymentReference = $paymentReference; note = 'TASK-012 installment'
    }
    $payment = Invoke-CoreJson "$apiBaseUrl/payments/$($payment.paymentId)" PUT -Token $adminToken -Body @{
        paymentDate = $attendanceDate; amount = 1500000; paymentMethod = 'BANK_TRANSFER'
        paymentReference = $paymentReference; status = 'COMPLETED'; note = 'Verified payment'
        rowVersion = $payment.rowVersion
    }
    Assert-True ($payment.completedPaid -eq 1500000) 'Completed payment total is incorrect.'
    Assert-True ($payment.outstandingAmount -eq 900000) 'Outstanding tuition is incorrect.'

    $overpayment = Invoke-CoreJson "$apiBaseUrl/payments" POST -Token $adminToken -Body @{
        enrollmentId = $enrollment.enrollmentId; paymentDate = $attendanceDate; amount = 1000000
        paymentMethod = 'CASH'; paymentReference = "CORE-OVER-$suffix"; note = 'Expected overpayment guard'
    }
    Invoke-ExpectedCoreProblem "$apiBaseUrl/payments/$($overpayment.paymentId)" PUT 409 -Token $adminToken -Body @{
        paymentDate = $attendanceDate; amount = 1000000; paymentMethod = 'CASH'
        paymentReference = "CORE-OVER-$suffix"; status = 'COMPLETED'; note = 'Expected rejection'
        rowVersion = $overpayment.rowVersion
    } | Out-Null
    $overpayment = Invoke-CoreJson "$apiBaseUrl/payments/$($overpayment.paymentId)" PUT -Token $adminToken -Body @{
        paymentDate = $attendanceDate; amount = 1000000; paymentMethod = 'CASH'
        paymentReference = "CORE-OVER-$suffix"; status = 'FAILED'; note = 'TASK-012 cleanup'
        rowVersion = $overpayment.rowVersion
    }
    Remove-CoreResource "$apiBaseUrl/payments/$($overpayment.paymentId)" $overpayment.rowVersion $adminToken
    $overpayment = $null

    $attendanceList = Invoke-CoreJson "$apiBaseUrl/attendance?page=1&pageSize=5&studentId=$($student.studentId)&classId=$($class.classId)&status=LATE&dateFrom=$attendanceDate&dateTo=$attendanceDate" GET -Token $adminToken
    Assert-True ($attendanceList.totalCount -eq 1) 'Attendance cross-module filters did not return one record.'
    Assert-True ($attendanceList.items[0].studentCode -eq $studentCode) 'Attendance did not resolve the expected student.'
    Assert-True ($attendanceList.items[0].classCode -eq $classCode) 'Attendance did not resolve the expected class.'

    $paymentList = Invoke-CoreJson "$apiBaseUrl/payments?page=1&pageSize=5&studentId=$($student.studentId)&classId=$($class.classId)&status=COMPLETED&paymentMethod=BANK_TRANSFER&search=$paymentReference" GET -Token $adminToken
    Assert-True ($paymentList.totalCount -eq 1) 'Payment cross-module filters did not return one record.'
    Assert-True ($paymentList.items[0].courseCode -eq $courseCode) 'Payment did not resolve the expected course.'

    $payment = Invoke-CoreJson "$apiBaseUrl/payments/$($payment.paymentId)" PUT -Token $adminToken -Body @{
        paymentDate = $attendanceDate; amount = 1500000; paymentMethod = 'BANK_TRANSFER'
        paymentReference = $paymentReference; status = 'REFUNDED'; note = 'TASK-012 cleanup'
        rowVersion = $payment.rowVersion
    }
    Remove-CoreResource "$apiBaseUrl/payments/$($payment.paymentId)" $payment.rowVersion $adminToken
    $payment = $null

    Remove-CoreResource "$apiBaseUrl/attendance/$($attendance.attendanceId)" $attendance.rowVersion $adminToken
    $attendance = $null

    $enrollment = Invoke-CoreJson "$apiBaseUrl/enrollments/$($enrollment.enrollmentId)" PUT -Token $adminToken -Body @{
        agreedTuition = 2400000; status = 'CANCELLED'; completionNote = 'TASK-012 cleanup'
        rowVersion = $enrollment.rowVersion
    }
    Remove-CoreResource "$apiBaseUrl/enrollments/$($enrollment.enrollmentId)" $enrollment.rowVersion $adminToken
    $enrollment = $null

    $cancelClassRequest = $classRequest.Clone()
    $cancelClassRequest.status = 'CANCELLED'
    $cancelClassRequest.rowVersion = $class.rowVersion
    $class = Invoke-CoreJson "$apiBaseUrl/classes/$($class.classId)" PUT -Token $adminToken -Body $cancelClassRequest
    Remove-CoreResource "$apiBaseUrl/classes/$($class.classId)" $class.rowVersion $adminToken
    $class = $null

    foreach ($userId in $createdUserIds) {
        $response = Invoke-CoreRequest "$apiBaseUrl/auth/users/$userId" DELETE -Token $adminToken
        Assert-True ($response.StatusCode -eq 204) "Auth user $userId was not deleted."
    }
    $createdUserIds.Clear()

    Remove-CoreResource "$apiBaseUrl/courses/$($course.courseId)" $course.rowVersion $adminToken
    $course = $null
    Remove-CoreResource "$apiBaseUrl/teachers/$($teacher.teacherId)" $teacher.rowVersion $adminToken
    $teacher = $null
    Remove-CoreResource "$apiBaseUrl/students/$($student.studentId)" $student.rowVersion $adminToken
    $student = $null

    foreach ($check in @(
        @{ Uri = "$apiBaseUrl/courses?page=1&pageSize=5&search=$courseCode"; Label = 'course' },
        @{ Uri = "$apiBaseUrl/classes?page=1&pageSize=5&search=$classCode"; Label = 'class' },
        @{ Uri = "$apiBaseUrl/students?page=1&pageSize=5&search=$studentCode"; Label = 'student' },
        @{ Uri = "$apiBaseUrl/teachers?page=1&pageSize=5&search=$teacherCode"; Label = 'teacher' },
        @{ Uri = "$apiBaseUrl/payments?page=1&pageSize=5&search=$paymentReference"; Label = 'payment' }
    )) {
        $result = Invoke-CoreJson $check.Uri GET -Token $adminToken
        Assert-True ($result.totalCount -eq 0) "The $($check.Label) fixture remains visible after cleanup."
    }

    $verificationSucceeded = $true
    [pscustomobject]@{
        Verification = 'PASS'
        SpaBusinessRoutes = 'PASS'
        FrontendProxyAuthentication = 'PASS'
        StudentToPaymentLifecycle = 'PASS'
        TeacherRbac = 'PASS'
        StudentRbac = 'PASS'
        AttendanceConcurrency409 = 'PASS'
        ValidationAndBusinessGuards = 'PASS'
        CrossModuleReadback = 'PASS'
        TargetedCleanup = 'PASS'
    }
}
finally {
    if (-not $verificationSucceeded -and -not [string]::IsNullOrWhiteSpace($adminToken)) {
        if ($null -ne $overpayment) {
            Invoke-BestEffortCleanup {
                $latest = Get-CoreResourceOrNull "$apiBaseUrl/payments/$($overpayment.paymentId)" $adminToken
                if ($null -ne $latest -and $latest.status -eq 'PENDING') {
                    $latest = Invoke-CoreJson "$apiBaseUrl/payments/$($latest.paymentId)" PUT -Token $adminToken -Body @{
                        paymentDate = $latest.paymentDate; amount = $latest.amount; paymentMethod = $latest.paymentMethod
                        paymentReference = $latest.paymentReference; status = 'FAILED'; note = 'TASK-012 recovery cleanup'
                        rowVersion = $latest.rowVersion
                    }
                }
                if ($null -ne $latest) { Remove-CoreResource "$apiBaseUrl/payments/$($latest.paymentId)" $latest.rowVersion $adminToken }
            } 'overpayment guard record'
        }
        if ($null -ne $payment) {
            Invoke-BestEffortCleanup {
                $latest = Get-CoreResourceOrNull "$apiBaseUrl/payments/$($payment.paymentId)" $adminToken
                if ($null -ne $latest -and $latest.status -eq 'COMPLETED') {
                    $latest = Invoke-CoreJson "$apiBaseUrl/payments/$($latest.paymentId)" PUT -Token $adminToken -Body @{
                        paymentDate = $latest.paymentDate; amount = $latest.amount; paymentMethod = $latest.paymentMethod
                        paymentReference = $latest.paymentReference; status = 'REFUNDED'; note = 'TASK-012 recovery cleanup'
                        rowVersion = $latest.rowVersion
                    }
                }
                if ($null -ne $latest) { Remove-CoreResource "$apiBaseUrl/payments/$($latest.paymentId)" $latest.rowVersion $adminToken }
            } 'payment'
        }
        if ($null -ne $attendance) {
            Invoke-BestEffortCleanup {
                $latest = Get-CoreResourceOrNull "$apiBaseUrl/attendance/$($attendance.attendanceId)" $adminToken
                if ($null -ne $latest) { Remove-CoreResource "$apiBaseUrl/attendance/$($latest.attendanceId)" $latest.rowVersion $adminToken }
            } 'attendance'
        }
        if ($null -ne $enrollment) {
            Invoke-BestEffortCleanup {
                $latest = Get-CoreResourceOrNull "$apiBaseUrl/enrollments/$($enrollment.enrollmentId)" $adminToken
                if ($null -ne $latest -and $latest.status -eq 'ACTIVE') {
                    $latest = Invoke-CoreJson "$apiBaseUrl/enrollments/$($latest.enrollmentId)" PUT -Token $adminToken -Body @{
                        agreedTuition = $latest.agreedTuition; status = 'CANCELLED'; completionNote = 'TASK-012 recovery cleanup'
                        rowVersion = $latest.rowVersion
                    }
                }
                if ($null -ne $latest) { Remove-CoreResource "$apiBaseUrl/enrollments/$($latest.enrollmentId)" $latest.rowVersion $adminToken }
            } 'enrollment'
        }
        if ($null -ne $class) {
            Invoke-BestEffortCleanup {
                $latest = Get-CoreResourceOrNull "$apiBaseUrl/classes/$($class.classId)" $adminToken
                if ($null -ne $latest -and $latest.status -ne 'CANCELLED') {
                    $cleanupClass = $classRequest.Clone()
                    $cleanupClass.status = 'CANCELLED'
                    $cleanupClass.rowVersion = $latest.rowVersion
                    $latest = Invoke-CoreJson "$apiBaseUrl/classes/$($latest.classId)" PUT -Token $adminToken -Body $cleanupClass
                }
                if ($null -ne $latest) { Remove-CoreResource "$apiBaseUrl/classes/$($latest.classId)" $latest.rowVersion $adminToken }
            } 'class'
        }
        foreach ($userId in $createdUserIds) {
            Invoke-BestEffortCleanup { Invoke-CoreRequest "$apiBaseUrl/auth/users/$userId" DELETE -Token $adminToken | Out-Null } "auth user $userId"
        }
        if ($null -ne $course) {
            Invoke-BestEffortCleanup {
                $latest = Get-CoreResourceOrNull "$apiBaseUrl/courses/$($course.courseId)" $adminToken
                if ($null -ne $latest) { Remove-CoreResource "$apiBaseUrl/courses/$($latest.courseId)" $latest.rowVersion $adminToken }
            } 'course'
        }
        if ($null -ne $teacher) {
            Invoke-BestEffortCleanup {
                $latest = Get-CoreResourceOrNull "$apiBaseUrl/teachers/$($teacher.teacherId)" $adminToken
                if ($null -ne $latest) { Remove-CoreResource "$apiBaseUrl/teachers/$($latest.teacherId)" $latest.rowVersion $adminToken }
            } 'teacher'
        }
        if ($null -ne $student) {
            Invoke-BestEffortCleanup {
                $latest = Get-CoreResourceOrNull "$apiBaseUrl/students/$($student.studentId)" $adminToken
                if ($null -ne $latest) { Remove-CoreResource "$apiBaseUrl/students/$($latest.studentId)" $latest.rowVersion $adminToken }
            } 'student'
        }
    }
}
