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
$today = [DateTime]::UtcNow.Date
$attendanceDate = $today.ToString('yyyy-MM-dd')
$startDate = $today.AddDays(-7).ToString('yyyy-MM-dd')
$endDate = $today.AddDays(30).ToString('yyyy-MM-dd')
$futureDate = $today.AddDays(31).ToString('yyyy-MM-dd')
$courseCode = "C10-$suffix"
$classCode = "CL10-$suffix"
$teacherCode = "T10-$suffix"
$studentCode = "S10-$suffix"
$paymentReference = "PAY-$suffix"

$course = Invoke-JsonRequest "$BaseUrl/api/courses" POST @{
    courseCode = $courseCode; courseName = 'TASK010 Course'; levelCode = 'B2'
    description = 'Attendance and payment acceptance'; plannedHours = 48; standardTuition = 2400000
}
$activeCourse = Invoke-JsonRequest "$BaseUrl/api/courses/$($course.courseId)" PUT @{
    courseCode = $courseCode; courseName = 'TASK010 Course'; levelCode = 'B2'
    description = 'Attendance and payment acceptance'; plannedHours = 48; standardTuition = 2400000
    status = 'ACTIVE'; rowVersion = $course.rowVersion
}
$teacher = Invoke-JsonRequest "$BaseUrl/api/teachers" POST @{
    teacherCode = $teacherCode; fullName = 'TASK010 Teacher'; email = "$($teacherCode.ToLowerInvariant())@example.test"
}
$student = Invoke-JsonRequest "$BaseUrl/api/students" POST @{
    studentCode = $studentCode; fullName = 'TASK010 Student'; email = "$($studentCode.ToLowerInvariant())@example.test"
}

$classRequest = @{
    classCode = $classCode; courseId = $activeCourse.courseId; teacherId = $teacher.teacherId
    className = 'TASK010 B2 Class'; startDate = $startDate; endDate = $endDate
    capacity = 10; scheduleNote = 'Acceptance schedule'; roomName = 'R-210'
}
$class = Invoke-JsonRequest "$BaseUrl/api/classes" POST $classRequest
$openRequest = $classRequest.Clone(); $openRequest.status = 'OPEN'; $openRequest.rowVersion = $class.rowVersion
$openClass = Invoke-JsonRequest "$BaseUrl/api/classes/$($class.classId)" PUT $openRequest
$enrollment = Invoke-JsonRequest "$BaseUrl/api/enrollments" POST @{
    studentId = $student.studentId; classId = $openClass.classId; agreedTuition = 2400000
}
$activeEnrollment = Invoke-JsonRequest "$BaseUrl/api/enrollments/$($enrollment.enrollmentId)" PUT @{
    agreedTuition = 2400000; status = 'ACTIVE'; completionNote = $null; rowVersion = $enrollment.rowVersion
}
$progressRequest = $classRequest.Clone(); $progressRequest.status = 'IN_PROGRESS'; $progressRequest.rowVersion = $openClass.rowVersion
$progressClass = Invoke-JsonRequest "$BaseUrl/api/classes/$($class.classId)" PUT $progressRequest

Invoke-ExpectedProblem "$BaseUrl/api/attendance" POST 400 @{
    enrollmentId = $activeEnrollment.enrollmentId; attendanceDate = $futureDate
    status = 'PRESENT'; checkInAt = "$($futureDate)T08:00:00Z"; recordedBy = 'acceptance'
} | Out-Null

$attendance = Invoke-JsonRequest "$BaseUrl/api/attendance" POST @{
    enrollmentId = $activeEnrollment.enrollmentId; attendanceDate = $attendanceDate
    status = 'PRESENT'; checkInAt = "$($attendanceDate)T08:00:00Z"; recordedBy = 'acceptance'
}
Assert-True ($attendance.status -eq 'PRESENT') 'Attendance did not preserve PRESENT status.'
Invoke-ExpectedProblem "$BaseUrl/api/attendance" POST 409 @{
    enrollmentId = $activeEnrollment.enrollmentId; attendanceDate = $attendanceDate
    status = 'LATE'; checkInAt = "$($attendanceDate)T08:15:00Z"; recordedBy = 'acceptance'
} | Out-Null

$absentAttendance = Invoke-JsonRequest "$BaseUrl/api/attendance/$($attendance.attendanceId)" PUT @{
    status = 'ABSENT'; checkInAt = $null; note = 'Updated by acceptance'; recordedBy = 'acceptance'
    rowVersion = $attendance.rowVersion
}
Assert-True ($absentAttendance.status -eq 'ABSENT') 'Attendance update did not persist.'
Invoke-ExpectedProblem "$BaseUrl/api/attendance/$($attendance.attendanceId)" PUT 409 @{
    status = 'LATE'; checkInAt = "$($attendanceDate)T08:15:00Z"; note = $null; recordedBy = 'acceptance'
    rowVersion = $attendance.rowVersion
} | Out-Null

$attendanceList = Invoke-JsonRequest "$BaseUrl/api/attendance?page=1&pageSize=5&studentId=$($student.studentId)&classId=$($progressClass.classId)&status=ABSENT&dateFrom=$attendanceDate&dateTo=$attendanceDate" GET
Assert-True ($attendanceList.totalCount -eq 1) 'Attendance filters did not return one record.'

$payment = Invoke-JsonRequest "$BaseUrl/api/payments" POST @{
    enrollmentId = $activeEnrollment.enrollmentId; paymentDate = $attendanceDate; amount = 1500000
    paymentMethod = 'BANK_TRANSFER'; paymentReference = $paymentReference; note = 'First installment'
}
Assert-True ($payment.status -eq 'PENDING') 'Payment did not start in PENDING.'
Invoke-ExpectedProblem "$BaseUrl/api/payments" POST 409 @{
    enrollmentId = $activeEnrollment.enrollmentId; paymentDate = $attendanceDate; amount = 100000
    paymentMethod = 'CASH'; paymentReference = $paymentReference
} | Out-Null

$completedPayment = Invoke-JsonRequest "$BaseUrl/api/payments/$($payment.paymentId)" PUT @{
    paymentDate = $attendanceDate; amount = 1500000; paymentMethod = 'BANK_TRANSFER'
    paymentReference = $paymentReference; status = 'COMPLETED'; note = 'Confirmed'; rowVersion = $payment.rowVersion
}
Assert-True ($completedPayment.completedPaid -eq 1500000) 'Completed payment total is incorrect.'
Assert-True ($completedPayment.outstandingAmount -eq 900000) 'Outstanding tuition is incorrect.'
Invoke-ExpectedProblem -Uri "$BaseUrl/api/payments/$($payment.paymentId)" -Method DELETE -ExpectedStatus 409 `
    -Headers @{ 'If-Match' = '"' + $completedPayment.rowVersion + '"' } | Out-Null

$secondPayment = Invoke-JsonRequest "$BaseUrl/api/payments" POST @{
    enrollmentId = $activeEnrollment.enrollmentId; paymentDate = $attendanceDate; amount = 1000000
    paymentMethod = 'CASH'; paymentReference = "PAY2-$suffix"
}
Invoke-ExpectedProblem "$BaseUrl/api/payments/$($secondPayment.paymentId)" PUT 409 @{
    paymentDate = $attendanceDate; amount = 1000000; paymentMethod = 'CASH'
    paymentReference = "PAY2-$suffix"; status = 'COMPLETED'; note = $null; rowVersion = $secondPayment.rowVersion
} | Out-Null
$failedPayment = Invoke-JsonRequest "$BaseUrl/api/payments/$($secondPayment.paymentId)" PUT @{
    paymentDate = $attendanceDate; amount = 1000000; paymentMethod = 'CASH'
    paymentReference = "PAY2-$suffix"; status = 'FAILED'; note = 'Acceptance cleanup'; rowVersion = $secondPayment.rowVersion
}
Remove-VersionedResource "$BaseUrl/api/payments/$($failedPayment.paymentId)" $failedPayment.rowVersion

$refundedPayment = Invoke-JsonRequest "$BaseUrl/api/payments/$($payment.paymentId)" PUT @{
    paymentDate = $attendanceDate; amount = 1500000; paymentMethod = 'BANK_TRANSFER'
    paymentReference = $paymentReference; status = 'REFUNDED'; note = 'Refunded by acceptance'
    rowVersion = $completedPayment.rowVersion
}
Assert-True ($refundedPayment.completedPaid -eq 0) 'Refund did not remove the amount from completed total.'
Invoke-ExpectedProblem "$BaseUrl/api/payments/$($payment.paymentId)" PUT 409 @{
    paymentDate = $attendanceDate; amount = 1500000; paymentMethod = 'BANK_TRANSFER'
    paymentReference = $paymentReference; status = 'REFUNDED'; note = 'Stale update'
    rowVersion = $completedPayment.rowVersion
} | Out-Null

$paymentList = Invoke-JsonRequest "$BaseUrl/api/payments?page=1&pageSize=5&studentId=$($student.studentId)&classId=$($progressClass.classId)&status=REFUNDED&paymentMethod=BANK_TRANSFER&search=$paymentReference" GET
Assert-True ($paymentList.totalCount -eq 1) 'Payment filters did not return one record.'

Remove-VersionedResource "$BaseUrl/api/payments/$($refundedPayment.paymentId)" $refundedPayment.rowVersion
Invoke-ExpectedProblem "$BaseUrl/api/payments" POST 409 @{
    enrollmentId = $activeEnrollment.enrollmentId; paymentDate = $attendanceDate; amount = 100000
    paymentMethod = 'CASH'; paymentReference = $paymentReference
} | Out-Null
Remove-VersionedResource "$BaseUrl/api/attendance/$($absentAttendance.attendanceId)" $absentAttendance.rowVersion

$cancelledEnrollment = Invoke-JsonRequest "$BaseUrl/api/enrollments/$($activeEnrollment.enrollmentId)" PUT @{
    agreedTuition = 2400000; status = 'CANCELLED'; completionNote = 'TASK010 cleanup'; rowVersion = $activeEnrollment.rowVersion
}
Remove-VersionedResource "$BaseUrl/api/enrollments/$($cancelledEnrollment.enrollmentId)" $cancelledEnrollment.rowVersion
$cancelRequest = $classRequest.Clone(); $cancelRequest.status = 'CANCELLED'; $cancelRequest.rowVersion = $progressClass.rowVersion
$cancelledClass = Invoke-JsonRequest "$BaseUrl/api/classes/$($progressClass.classId)" PUT $cancelRequest
Remove-VersionedResource "$BaseUrl/api/classes/$($cancelledClass.classId)" $cancelledClass.rowVersion
Remove-VersionedResource "$BaseUrl/api/courses/$($activeCourse.courseId)" $activeCourse.rowVersion
Remove-VersionedResource "$BaseUrl/api/teachers/$($teacher.teacherId)" $teacher.rowVersion
Remove-VersionedResource "$BaseUrl/api/students/$($student.studentId)" $student.rowVersion

$openApi = Invoke-JsonRequest "$BaseUrl/openapi/v1.json" GET
foreach ($path in @('/api/attendance', '/api/attendance/{attendanceId}', '/api/payments', '/api/payments/{paymentId}')) {
    Assert-True ($null -ne $openApi.paths.$path) "OpenAPI is missing $path."
}

[pscustomobject]@{
    Verification = 'PASS'
    AttendanceCrud = 'PASS'
    AttendanceDateGuard400 = 'PASS'
    DuplicateAttendance409 = 'PASS'
    AttendanceStaleWrite409 = 'PASS'
    PaymentLifecycle = 'PASS'
    DuplicateReference409 = 'PASS'
    Overpayment409 = 'PASS'
    CompletedPaymentDelete409 = 'PASS'
    PaymentStaleWrite409 = 'PASS'
    SearchAndFilters = 'PASS'
    SoftDeleteCleanup = 'PASS'
    OpenApiOperationsPaths = 'PASS'
}
