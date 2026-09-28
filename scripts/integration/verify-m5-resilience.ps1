[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^https?://[^\s/]+(?::\d+)?$')]
    [string]$BaseUrl = 'http://localhost:8080'
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

function Invoke-Compose {
    param([Parameter(Mandatory)][string[]]$Arguments)

    $output = & docker compose --env-file $environmentFile -f $composeFile @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Docker Compose failed with exit code $LASTEXITCODE while running: $($Arguments -join ' ')"
    }
    return $output
}

function Convert-ResponseContent {
    param([Parameter(Mandatory)]$Response)

    $json = if ($Response.Content -is [byte[]]) {
        [Text.Encoding]::UTF8.GetString($Response.Content)
    }
    else {
        [string]$Response.Content
    }
    return $json | ConvertFrom-Json
}

function Get-HealthResponse {
    return Invoke-WebRequest -UseBasicParsing -SkipHttpErrorCheck -Uri "$BaseUrl/api/health" -Method GET
}

function Wait-DependencyState {
    param(
        [Parameter(Mandatory)][ValidateSet('redis', 'rabbitmq')][string]$Dependency,
        [Parameter(Mandatory)][ValidateSet('Healthy', 'Unhealthy')][string]$Expected
    )

    for ($attempt = 1; $attempt -le 20; $attempt++) {
        try {
            $response = Get-HealthResponse
            $health = Convert-ResponseContent $response
            $check = $health.checks | Where-Object name -eq $Dependency
            if ($null -ne $check -and $check.status -eq $Expected) {
                if ($Expected -eq 'Healthy' -and $response.StatusCode -ne 200) {
                    Start-Sleep -Seconds 2
                    continue
                }
                if ($Expected -eq 'Unhealthy' -and $response.StatusCode -ne 503) {
                    Start-Sleep -Seconds 2
                    continue
                }
                return $health
            }
        }
        catch {
            if ($attempt -eq 20) { throw }
        }
        Start-Sleep -Seconds 2
    }
    throw "$Dependency did not become $Expected within the allowed checks."
}

function Invoke-AuthorizedCourseRead {
    $response = Invoke-WebRequest -UseBasicParsing -SkipHttpErrorCheck `
        -Uri "$BaseUrl/api/courses?page=1&pageSize=1" -Method GET `
        -Headers (Merge-AuthorizationHeaders)
    Assert-True ($response.StatusCode -eq 200) 'SQL-backed course read did not return HTTP 200.'
}

function Wait-NotificationConsumer {
    for ($attempt = 1; $attempt -le 30; $attempt++) {
        $json = Invoke-Compose @(
            'exec', '-T', 'rabbitmq',
            'rabbitmqctl', 'list_queues', '--quiet', 'name', 'consumers', '--formatter', 'json'
        ) | Out-String
        try {
            $queues = $json | ConvertFrom-Json
            $dispatch = $queues | Where-Object name -eq 'english-center.notifications.dispatch'
            if ($null -ne $dispatch -and [int]$dispatch.consumers -ge 1) { return }
        }
        catch {
            if ($attempt -eq 30) { throw }
        }
        Start-Sleep -Seconds 2
    }
    throw 'Notification worker did not re-register a dispatch-queue consumer after RabbitMQ recovery.'
}

if (-not (Test-Path -LiteralPath $environmentFile)) {
    throw 'Missing infrastructure/.env. Copy infrastructure/.env.example and provide local secret values.'
}

$script:AccessToken = Get-AdminAuthToken $BaseUrl
$redisStopped = $false
$rabbitMqStopped = $false

try {
    $baseline = Convert-ResponseContent (Get-HealthResponse)
    Assert-True ($baseline.status -eq 'Healthy') 'M5 baseline health is not Healthy.'
    Invoke-AuthorizedCourseRead

    Invoke-Compose @('stop', 'redis') | Out-Null
    $redisStopped = $true
    Wait-DependencyState redis Unhealthy | Out-Null
    Invoke-AuthorizedCourseRead

    $uniqueEmail = "m5-redis-outage-$([guid]::NewGuid().ToString('N'))@example.test"
    $loginBody = @{ email = $uniqueEmail; password = 'Wrong-Pass-123!' } | ConvertTo-Json
    $loginDuringOutage = Invoke-WebRequest -UseBasicParsing -SkipHttpErrorCheck `
        -Uri "$BaseUrl/api/auth/login" -Method POST -ContentType 'application/json' -Body $loginBody
    Assert-True ($loginDuringOutage.StatusCode -eq 401) 'Login did not fail open to normal authentication while Redis was unavailable.'

    Invoke-Compose @('start', 'redis') | Out-Null
    $redisStopped = $false
    Wait-DependencyState redis Healthy | Out-Null
    & (Join-Path $repositoryRoot 'scripts\cache\verify-redis-cache.ps1') -BaseUrl $BaseUrl | Out-Host
    & (Join-Path $repositoryRoot 'scripts\cache\verify-redis-rate-limiting.ps1') -BaseUrl $BaseUrl | Out-Host

    Invoke-Compose @('stop', 'rabbitmq') | Out-Null
    $rabbitMqStopped = $true
    Wait-DependencyState rabbitmq Unhealthy | Out-Null
    Invoke-AuthorizedCourseRead

    $notificationBody = @{
        recipientUserId = 1
        channel = 'EMAIL'
        templateKey = 'm5.resilience.probe'
        parameters = @{ source = 'TASK-018' }
    } | ConvertTo-Json -Depth 4
    $publishDuringOutage = Invoke-WebRequest -UseBasicParsing -SkipHttpErrorCheck `
        -Uri "$BaseUrl/api/notifications" -Method POST `
        -Headers (Merge-AuthorizationHeaders) -ContentType 'application/json' -Body $notificationBody
    Assert-True ($publishDuringOutage.StatusCode -eq 503) 'Notification publish did not return HTTP 503 while RabbitMQ was unavailable.'
    $publishProblem = Convert-ResponseContent $publishDuringOutage
    Assert-True ($publishProblem.status -eq 503) 'RabbitMQ outage response is not Problem Details HTTP 503.'

    Invoke-Compose @('start', 'rabbitmq') | Out-Null
    $rabbitMqStopped = $false
    Wait-DependencyState rabbitmq Healthy | Out-Null
    Wait-NotificationConsumer
    & (Join-Path $repositoryRoot 'scripts\messaging\verify-rabbitmq-topology.ps1') -ApiBaseUrl $BaseUrl | Out-Host
    & (Join-Path $repositoryRoot 'scripts\messaging\verify-notification-worker.ps1') | Out-Host

    [pscustomobject]@{
        Verification = 'PASS'
        BaselineHealth = 'PASS'
        RedisOutageDetected = 'HTTP 503 health'
        SqlFallbackWithoutRedis = 'PASS'
        RateLimiterFailOpen = 'PASS'
        RedisRecovery = 'PASS'
        RabbitMqOutageDetected = 'HTTP 503 health'
        NonMessagingApiDuringRabbitMqOutage = 'PASS'
        NotificationDependencyError = 'HTTP 503 Problem Details'
        WorkerConsumerRecovery = 'PASS'
        CacheAndRateLimitAfterRecovery = 'PASS'
        MessagingRetryDlqAfterRecovery = 'PASS'
    }
}
finally {
    if ($redisStopped -or $rabbitMqStopped) {
        try {
            Invoke-Compose @('up', '-d', 'redis', 'rabbitmq', 'api') | Out-Null
        }
        catch {
            Write-Warning 'Automatic dependency recovery did not complete. Run docker compose up for redis, rabbitmq, and api.'
        }
    }
}
