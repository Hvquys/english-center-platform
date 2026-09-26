[CmdletBinding()]
param(
    [ValidatePattern('^https?://[^\s/]+(?::\d+)?$')]
    [string]$ManagementBaseUrl = 'http://localhost:15672',

    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]{0,127}$')]
    [string]$DatabaseName = 'EnglishCenter'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$environmentFile = Join-Path $repositoryRoot 'infrastructure\.env'
$composeFile = Join-Path $repositoryRoot 'infrastructure\docker-compose.yml'
$mainExchange = 'english-center.notifications'
$dispatchQueue = 'english-center.notifications.dispatch'
$retryQueue = 'english-center.notifications.retry'
$deadLetterQueue = 'english-center.notifications.dead-letter'
$routingKey = 'notification.email.requested'

function Assert-True {
    param([Parameter(Mandatory)][bool]$Condition, [Parameter(Mandatory)][string]$Message)
    if (-not $Condition) { throw $Message }
}

function Get-LocalSettings {
    $settings = @{}
    foreach ($line in Get-Content -LiteralPath $environmentFile) {
        $trimmedLine = $line.Trim()
        if ($trimmedLine.Length -eq 0 -or $trimmedLine.StartsWith('#')) { continue }
        $name, $value = $trimmedLine -split '=', 2
        if ($null -ne $value) { $settings[$name.Trim()] = $value.Trim() }
    }
    return $settings
}

function Invoke-RabbitManagement {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter()][ValidateSet('GET', 'POST')][string]$Method = 'GET',
        [Parameter()][object]$Body
    )

    $parameters = @{
        UseBasicParsing = $true
        Uri = "$ManagementBaseUrl$Path"
        Method = $Method
        Headers = $script:RabbitHeaders
    }
    if ($PSBoundParameters.ContainsKey('Body')) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = $Body | ConvertTo-Json -Depth 15 -Compress
    }
    $response = Invoke-WebRequest @parameters
    if ([string]::IsNullOrWhiteSpace($response.Content)) { return $null }
    return $response.Content | ConvertFrom-Json
}

function Get-Queue {
    param([Parameter(Mandatory)][string]$QueueName)
    $encodedQueue = [uri]::EscapeDataString($QueueName)
    return Invoke-RabbitManagement -Path "/api/queues/%2F/$encodedQueue"
}

function Get-OneMessage {
    param([Parameter(Mandatory)][string]$QueueName)
    $encodedQueue = [uri]::EscapeDataString($QueueName)
    $result = Invoke-RabbitManagement -Path "/api/queues/%2F/$encodedQueue/get" -Method POST -Body @{
        count = 1
        ackmode = 'ack_requeue_false'
        encoding = 'auto'
        truncate = 50000
    }
    return @($result)
}

function Publish-TestEvent {
    param([Parameter(Mandatory)][hashtable]$Event)
    $encodedExchange = [uri]::EscapeDataString($mainExchange)
    $result = Invoke-RabbitManagement -Path "/api/exchanges/%2F/$encodedExchange/publish" -Method POST -Body @{
        properties = @{
            delivery_mode = 2
            message_id = $Event.eventId
            content_type = 'application/json'
            type = 'NotificationRequestedIntegrationEvent'
        }
        routing_key = $routingKey
        payload = $Event | ConvertTo-Json -Depth 10 -Compress
        payload_encoding = 'string'
    }
    Assert-True ($result.routed -eq $true) "RabbitMQ did not route event $($Event.eventId)."
}

function Invoke-SqlScalar {
    param([Parameter(Mandatory)][string]$Query)
    $output = & docker exec --env $script:SqlPasswordArgument $script:SqlContainer `
        /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -d $DatabaseName -h -1 -W -Q "SET NOCOUNT ON; $Query" 2>&1
    if ($LASTEXITCODE -ne 0) { throw "SQL verification failed: $output" }
    return (($output | Out-String).Trim())
}

if (-not (Test-Path -LiteralPath $environmentFile)) { throw "Missing $environmentFile." }
$settings = Get-LocalSettings
foreach ($name in 'RABBITMQ_USER', 'RABBITMQ_PASSWORD', 'SQLSERVER_SA_PASSWORD') {
    if (-not $settings.ContainsKey($name) -or [string]::IsNullOrWhiteSpace($settings[$name])) {
        throw "Missing $name in infrastructure/.env."
    }
}

$basicBytes = [Text.Encoding]::UTF8.GetBytes("$($settings.RABBITMQ_USER):$($settings.RABBITMQ_PASSWORD)")
$script:RabbitHeaders = @{ Authorization = "Basic $([Convert]::ToBase64String($basicBytes))" }
$script:SqlPasswordArgument = "SQLCMDPASSWORD=$($settings.SQLSERVER_SA_PASSWORD)"
$script:SqlContainer = (& docker compose --env-file $environmentFile -f $composeFile ps -q sqlserver).Trim()
Assert-True (-not [string]::IsNullOrWhiteSpace($script:SqlContainer)) 'SQL Server Compose container is not running.'

$dispatch = Get-Queue $dispatchQueue
$retry = Get-Queue $retryQueue
$deadLetter = Get-Queue $deadLetterQueue
Assert-True ($dispatch.consumers -ge 1) 'Notification worker is not attached to the dispatch queue.'
Assert-True ($dispatch.messages -eq 0 -and $retry.messages -eq 0 -and $deadLetter.messages -eq 0) 'Notification queues must be empty before the worker acceptance test.'

$validEventId = [guid]::NewGuid()
$invalidEventId = [guid]::NewGuid()
$now = [DateTimeOffset]::UtcNow.ToString('o')
$validEvent = @{
    schemaVersion = 1
    eventId = $validEventId.ToString()
    occurredAtUtc = $now
    recipientUserId = 900015
    channel = 'EMAIL'
    templateKey = 'task015.acceptance'
    parameters = @{ testRun = $validEventId.ToString('N') }
    correlationId = "task015-$($validEventId.ToString('N'))"
}
$invalidEvent = $validEvent.Clone()
$invalidEvent.schemaVersion = 999
$invalidEvent.eventId = $invalidEventId.ToString()
$invalidEvent.correlationId = "task015-$($invalidEventId.ToString('N'))"

try {
    Publish-TestEvent $validEvent
    Publish-TestEvent $validEvent

    $validCount = '0'
    for ($attempt = 0; $attempt -lt 30 -and $validCount -ne '1'; $attempt++) {
        Start-Sleep -Milliseconds 200
        $validCount = Invoke-SqlScalar "SELECT COUNT_BIG(*) FROM dbo.NotificationProcessingRecords WHERE event_id = '$validEventId';"
    }
    Assert-True ($validCount -eq '1') 'Duplicate deliveries were not reduced to one durable processing record.'

    Publish-TestEvent $invalidEvent
    $deadMessages = @()
    for ($attempt = 0; $attempt -lt 50 -and $deadMessages.Count -eq 0; $attempt++) {
        Start-Sleep -Milliseconds 250
        $deadMessages = Get-OneMessage $deadLetterQueue
    }
    Assert-True ($deadMessages.Count -eq 1) 'Invalid event did not reach the dead-letter queue after finite retries.'
    $deadPayload = $deadMessages[0].payload | ConvertFrom-Json
    Assert-True ($deadPayload.eventId -eq $invalidEventId.ToString()) 'Dead-letter queue contains an unexpected event.'
    Assert-True ([int]$deadMessages[0].properties.headers.'x-retry-count' -eq 3) 'Invalid event did not complete exactly three delayed retries.'

    $invalidCount = Invoke-SqlScalar "SELECT COUNT_BIG(*) FROM dbo.NotificationProcessingRecords WHERE event_id = '$invalidEventId';"
    Assert-True ($invalidCount -eq '0') 'Invalid event was written to the processing ledger.'
}
finally {
    Invoke-SqlScalar "DELETE FROM dbo.NotificationProcessingRecords WHERE event_id IN ('$validEventId', '$invalidEventId'); SELECT 1;" | Out-Null
}

$dispatchAfter = Get-Queue $dispatchQueue
$retryAfter = Get-Queue $retryQueue
$deadLetterAfter = Get-Queue $deadLetterQueue
Assert-True ($dispatchAfter.messages -eq 0 -and $retryAfter.messages -eq 0 -and $deadLetterAfter.messages -eq 0) 'Acceptance queues were not empty after cleanup.'

[pscustomobject]@{
    Verification       = 'PASS'
    WorkerConsumer     = 'PASS'
    PersistentDelivery = 'PASS'
    DuplicateEventId   = 'PASS'
    DelayedRetry       = 'PASS'
    RetryCount         = '3/3'
    DeadLetterQueue    = 'PASS'
    TargetedCleanup    = 'PASS'
}
