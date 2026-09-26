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
$testKey = $null

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

function Get-RateLimitKeys {
    $text = Invoke-Redis "--scan --pattern 'english-center:rate-limit:login:*'"
    if ([string]::IsNullOrWhiteSpace($text)) { return @() }
    return @($text -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Invoke-TestLogin {
    param([Parameter(Mandatory)][string]$Email)

    $body = @{ email = $Email; password = 'Wrong-Pass-123!' } | ConvertTo-Json
    return Invoke-WebRequest -UseBasicParsing -SkipHttpErrorCheck `
        -Uri "$BaseUrl/api/auth/login" -Method POST `
        -ContentType 'application/json' -Body $body
}

if (-not (Test-Path -LiteralPath $environmentFile)) {
    throw 'Missing infrastructure/.env. Copy infrastructure/.env.example and provide local secret values.'
}

$health = Invoke-RestMethod -Uri "$BaseUrl/api/health" -Method GET
Assert-True ($health.status -eq 'Healthy') 'API health is not Healthy.'
Assert-True (($health.checks | Where-Object name -eq 'redis').status -eq 'Healthy') 'Redis health check is not Healthy.'

$openApi = Invoke-RestMethod -Uri "$BaseUrl/openapi/v1.json" -Method GET
Assert-True ($null -ne $openApi.paths.'/api/auth/login'.post.responses.'429') 'Login OpenAPI does not document HTTP 429.'
Assert-True ($null -ne $openApi.paths.'/api/auth/refresh'.post.responses.'429') 'Refresh OpenAPI does not document HTTP 429.'
Assert-True ($null -ne $openApi.paths.'/api/notifications'.post.responses.'429') 'Notification OpenAPI does not document HTTP 429.'

$email = "rate-limit-$([guid]::NewGuid().ToString('N'))@example.test"
$keysBefore = @(Get-RateLimitKeys)

try {
    for ($requestNumber = 1; $requestNumber -le 20; $requestNumber++) {
        $response = Invoke-TestLogin $email
        Assert-True ($response.StatusCode -eq 401) "Login request $requestNumber should return HTTP 401 before the limit."
    }

    $blocked = Invoke-TestLogin $email
    Assert-True ($blocked.StatusCode -eq 429) 'The twenty-first login request was not rate limited.'
    Assert-True (($blocked.Headers.'X-RateLimit-Limit' -join ',') -eq '20') 'Blocked response has an incorrect limit header.'
    Assert-True (($blocked.Headers.'X-RateLimit-Remaining' -join ',') -eq '0') 'Blocked response has an incorrect remaining header.'
    $retryAfter = [int]($blocked.Headers.'Retry-After' | Select-Object -First 1)
    Assert-True ($retryAfter -ge 1 -and $retryAfter -le 60) 'Retry-After is outside 1..60 seconds.'
    Assert-True ((($blocked.Headers.'Content-Type' -join ',') -match '^application/problem\+json')) 'Blocked response is not Problem Details JSON.'
    $problemJson = if ($blocked.Content -is [byte[]]) {
        [Text.Encoding]::UTF8.GetString($blocked.Content)
    }
    else {
        [string]$blocked.Content
    }
    $problem = $problemJson | ConvertFrom-Json
    Assert-True ($problem.status -eq 429 -and $problem.policy -eq 'login') 'Blocked Problem Details payload is incorrect.'
    Assert-True ($problem.limit -eq 20 -and $problem.retryAfterSeconds -eq $retryAfter) 'Blocked Problem Details metadata is incorrect.'
    Assert-True (-not [string]::IsNullOrWhiteSpace($problem.traceId)) 'Blocked Problem Details does not contain a trace id.'

    $keysAfter = @(Get-RateLimitKeys)
    $newKeys = @($keysAfter | Where-Object { $_ -notin $keysBefore })
    Assert-True ($newKeys.Count -eq 1) "Expected one new hashed Redis key but found $($newKeys.Count)."
    $testKey = $newKeys[0]
    Assert-True ($testKey -match '^english-center:rate-limit:login:[0-9a-f]{64}$') 'Redis rate-limit key does not use a SHA-256 partition hash.'
    $ttl = [int](Invoke-Redis "PTTL '$testKey'")
    Assert-True ($ttl -gt 0 -and $ttl -le 60000) 'Redis rate-limit key TTL is outside 1..60000 milliseconds.'

    Assert-True ((Invoke-Redis "PEXPIRE '$testKey' 1000") -eq '1') 'Could not shorten the test window.'
    Start-Sleep -Milliseconds 1500
    $afterWindow = Invoke-TestLogin $email
    Assert-True ($afterWindow.StatusCode -eq 401) 'Login was not accepted by the limiter after the window expired.'
    Assert-True ((Invoke-Redis "GET '$testKey'") -eq '1') 'Counter did not restart at one after the window expired.'

    [pscustomobject]@{
        Verification = 'PASS'
        RedisHealth = 'PASS'
        AtomicFixedWindow = 'PASS'
        TwentyFirstRequest = 'HTTP 429'
        ProblemDetails = 'PASS'
        RetryAfter = 'PASS'
        HashedPartitionKey = 'PASS'
        WindowReset = 'PASS'
        OpenApi429 = 'PASS'
    }
}
finally {
    if (-not [string]::IsNullOrWhiteSpace($testKey)) {
        Invoke-Redis "DEL '$testKey'" | Out-Null
        Assert-True ((Invoke-Redis "EXISTS '$testKey'") -eq '0') 'Test rate-limit key was not removed.'
    }
}
