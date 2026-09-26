[CmdletBinding()]
param(
    [ValidatePattern('^https?://[^\s/]+(?::\d+)?$')]
    [string]$ApiBaseUrl = 'http://localhost:8080',

    [ValidatePattern('^https?://[^\s/]+(?::\d+)?$')]
    [string]$ManagementBaseUrl = 'http://localhost:15672'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\api\auth-test-support.ps1')

$exchangeName = 'english-center.notifications'
$dispatchQueue = 'english-center.notifications.dispatch'
$dispatchBinding = 'notification.*.requested'
$deadLetterExchange = 'english-center.notifications.dlx'
$deadLetterQueue = 'english-center.notifications.dead-letter'
$deadLetterRoutingKey = 'notification.dead-letter'

function Assert-True {
    param([Parameter(Mandatory)][bool]$Condition, [Parameter(Mandatory)][string]$Message)
    if (-not $Condition) { throw $Message }
}

function ConvertTo-PathSegment {
    param([Parameter(Mandatory)][string]$Value)
    return [uri]::EscapeDataString($Value).Replace('%2F', '%2F')
}

function Invoke-RabbitManagement {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter()][ValidateSet('GET', 'POST', 'DELETE')][string]$Method = 'GET',
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
        $parameters.Body = $Body | ConvertTo-Json -Depth 10 -Compress
    }

    $response = Invoke-WebRequest @parameters
    if ([string]::IsNullOrWhiteSpace($response.Content)) { return $null }
    return $response.Content | ConvertFrom-Json
}

function Invoke-ExpectedApiStatus {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][int]$ExpectedStatus,
        [Parameter(Mandatory)][object]$Body,
        [Parameter()][hashtable]$Headers
    )

    try {
        $parameters = @{
            UseBasicParsing = $true
            Uri = "$ApiBaseUrl$Path"
            Method = 'POST'
            ContentType = 'application/json'
            Body = $Body | ConvertTo-Json -Depth 10 -Compress
        }
        if ($Headers) { $parameters.Headers = $Headers }
        $response = Invoke-WebRequest @parameters
    }
    catch [Microsoft.PowerShell.Commands.HttpResponseException] {
        $response = $_.Exception.Response
    }

    Assert-True ($response.StatusCode -eq $ExpectedStatus) "Expected HTTP $ExpectedStatus from $Path, received $($response.StatusCode)."
    return $response
}

function Get-Queue {
    param([Parameter(Mandatory)][string]$QueueName)
    $encodedQueue = ConvertTo-PathSegment $QueueName
    return Invoke-RabbitManagement -Path "/api/queues/%2F/$encodedQueue"
}

function Get-OneMessage {
    param(
        [Parameter(Mandatory)][string]$QueueName,
        [Parameter(Mandatory)][ValidateSet('ack_requeue_false', 'reject_requeue_false')][string]$AckMode
    )

    $encodedQueue = ConvertTo-PathSegment $QueueName
    $result = Invoke-RabbitManagement -Path "/api/queues/%2F/$encodedQueue/get" -Method POST -Body @{
        count = 1
        ackmode = $AckMode
        encoding = 'auto'
        truncate = 50000
    }
    return @($result)
}

$rabbitUser = Get-LocalEnvironmentValue 'RABBITMQ_USER'
$rabbitPassword = Get-LocalEnvironmentValue 'RABBITMQ_PASSWORD'
$basicBytes = [System.Text.Encoding]::UTF8.GetBytes("${rabbitUser}:${rabbitPassword}")
$script:RabbitHeaders = @{ Authorization = "Basic $([Convert]::ToBase64String($basicBytes))" }

$health = $null
for ($attempt = 0; $attempt -lt 30 -and $null -eq $health; $attempt++) {
    try {
        $health = Invoke-RestMethod -Uri "$ApiBaseUrl/api/health"
    }
    catch {
        Start-Sleep -Seconds 2
    }
}
Assert-True ($null -ne $health) 'API health did not become available within 60 seconds.'
$rabbitHealth = $health.checks | Where-Object name -eq 'rabbitmq'
Assert-True ($health.status -eq 'Healthy') 'API health is not Healthy.'
Assert-True ($null -ne $rabbitHealth -and $rabbitHealth.status -eq 'Healthy') 'RabbitMQ health check is not Healthy.'

$openApi = Invoke-RestMethod -Uri "$ApiBaseUrl/openapi/v1.json"
Assert-True ($null -ne $openApi.paths.'/api/notifications'.post) 'OpenAPI does not contain POST /api/notifications.'

$encodedExchange = ConvertTo-PathSegment $exchangeName
$encodedDeadLetterExchange = ConvertTo-PathSegment $deadLetterExchange
$encodedDispatchQueue = ConvertTo-PathSegment $dispatchQueue
$encodedDeadLetterQueue = ConvertTo-PathSegment $deadLetterQueue

$exchange = Invoke-RabbitManagement -Path "/api/exchanges/%2F/$encodedExchange"
$deadExchange = Invoke-RabbitManagement -Path "/api/exchanges/%2F/$encodedDeadLetterExchange"
$dispatch = Get-Queue $dispatchQueue
$deadLetter = Get-Queue $deadLetterQueue
Assert-True ($exchange.type -eq 'topic' -and $exchange.durable) 'Notification exchange is not a durable topic exchange.'
Assert-True ($deadExchange.type -eq 'direct' -and $deadExchange.durable) 'Dead-letter exchange is not a durable direct exchange.'
Assert-True ($dispatch.durable -and -not $dispatch.auto_delete) 'Dispatch queue is not durable.'
Assert-True ($deadLetter.durable -and -not $deadLetter.auto_delete) 'Dead-letter queue is not durable.'
Assert-True ($dispatch.arguments.'x-dead-letter-exchange' -eq $deadLetterExchange) 'Dispatch queue does not target the dead-letter exchange.'
Assert-True ($dispatch.arguments.'x-dead-letter-routing-key' -eq $deadLetterRoutingKey) 'Dispatch queue does not use the expected dead-letter routing key.'

$dispatchBindings = Invoke-RabbitManagement -Path "/api/bindings/%2F/e/$encodedExchange/q/$encodedDispatchQueue"
$deadBindings = Invoke-RabbitManagement -Path "/api/bindings/%2F/e/$encodedDeadLetterExchange/q/$encodedDeadLetterQueue"
Assert-True (@($dispatchBindings | Where-Object routing_key -eq $dispatchBinding).Count -eq 1) 'Dispatch binding is missing.'
Assert-True (@($deadBindings | Where-Object routing_key -eq $deadLetterRoutingKey).Count -eq 1) 'Dead-letter binding is missing.'

Assert-True ($dispatch.messages -eq 0) 'Dispatch queue must be empty before the acceptance test.'
Assert-True ($deadLetter.messages -eq 0) 'Dead-letter queue must be empty before the acceptance test.'

$testRequest = @{
    recipientUserId = 900014
    channel = 'EMAIL'
    templateKey = 'task014.acceptance'
    parameters = @{ testRun = [guid]::NewGuid().ToString('N') }
}
$unauthorized = Invoke-ExpectedApiStatus -Path '/api/notifications' -ExpectedStatus 401 -Body $testRequest
Assert-True ($unauthorized.Content.Headers.ContentType -match 'application/problem\+json') 'Unauthorized publish did not return Problem Details.'

$invalidRequest = $testRequest.Clone()
$invalidRequest.channel = 'SMS'
$script:AccessToken = Get-AdminAuthToken -BaseUrl $ApiBaseUrl
$invalid = Invoke-ExpectedApiStatus -Path '/api/notifications' -ExpectedStatus 400 -Body $invalidRequest -Headers (Merge-AuthorizationHeaders)
Assert-True ($invalid.Content.Headers.ContentType -match 'application/problem\+json') 'Invalid notification did not return Problem Details.'

$missingParameters = $testRequest.Clone()
$missingParameters.parameters = $null
$missingParametersResponse = Invoke-ExpectedApiStatus -Path '/api/notifications' -ExpectedStatus 400 -Body $missingParameters -Headers (Merge-AuthorizationHeaders)
Assert-True ($missingParametersResponse.Content.Headers.ContentType -match 'application/problem\+json') 'Null parameters did not return Problem Details.'

$acceptedResponse = Invoke-WebRequest -UseBasicParsing -Uri "$ApiBaseUrl/api/notifications" -Method POST `
    -Headers (Merge-AuthorizationHeaders) -ContentType 'application/json' `
    -Body ($testRequest | ConvertTo-Json -Depth 10 -Compress)
Assert-True ($acceptedResponse.StatusCode -eq 202) 'Notification API did not return HTTP 202.'
$accepted = $acceptedResponse.Content | ConvertFrom-Json
Assert-True ($accepted.routingKey -eq 'notification.email.requested') 'Notification API returned the wrong routing key.'

$dispatchMessages = @()
for ($attempt = 0; $attempt -lt 10 -and $dispatchMessages.Count -eq 0; $attempt++) {
    Start-Sleep -Milliseconds 200
    $dispatchMessages = Get-OneMessage -QueueName $dispatchQueue -AckMode 'reject_requeue_false'
}
Assert-True ($dispatchMessages.Count -eq 1) 'Published notification did not reach the dispatch queue.'
$published = $dispatchMessages[0]
$publishedPayload = $published.payload | ConvertFrom-Json
Assert-True ($published.routing_key -eq 'notification.email.requested') 'Published notification has the wrong routing key.'
Assert-True ($published.properties.delivery_mode -eq 2) 'Published notification is not persistent.'
Assert-True ($publishedPayload.eventId -eq $accepted.eventId) 'Published event id does not match the accepted response.'
Assert-True ($publishedPayload.recipientUserId -eq 900014) 'Published event has the wrong synthetic recipient id.'

$deadMessages = @()
for ($attempt = 0; $attempt -lt 10 -and $deadMessages.Count -eq 0; $attempt++) {
    Start-Sleep -Milliseconds 200
    $deadMessages = Get-OneMessage -QueueName $deadLetterQueue -AckMode 'ack_requeue_false'
}
Assert-True ($deadMessages.Count -eq 1) 'Rejected notification did not reach the dead-letter queue.'
$deadPayload = $deadMessages[0].payload | ConvertFrom-Json
Assert-True ($deadPayload.eventId -eq $accepted.eventId) 'Dead-lettered event id does not match the published event.'

$dispatchAfter = Get-Queue $dispatchQueue
$deadLetterAfter = Get-Queue $deadLetterQueue
Assert-True ($dispatchAfter.messages -eq 0 -and $deadLetterAfter.messages -eq 0) 'Acceptance queues were not clean after verification.'

[pscustomobject]@{
    Verification          = 'PASS'
    RabbitMqHealth        = 'PASS'
    OpenApiNotification   = 'PASS'
    DurableTopicExchange  = 'PASS'
    DurableDispatchQueue  = 'PASS'
    DispatchBinding       = 'PASS'
    DeadLetterTopology    = 'PASS'
    Unauthorized401       = 'PASS'
    InvalidRequest400     = 'PASS'
    NullParameters400     = 'PASS'
    AuthorizedPublish202  = 'PASS'
    PublisherConfirmRoute = 'PASS'
    DeadLetterRoute       = 'PASS'
    QueueCleanup          = 'PASS'
}
