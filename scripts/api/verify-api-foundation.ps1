[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^https?://[^\s/]+(?::\d+)?$')]
    [string]$BaseUrl = 'http://localhost:8080'
)

$ErrorActionPreference = 'Stop'

function Invoke-ExpectedHttpError {
    param(
        [Parameter(Mandatory)]
        [string]$Uri,

        [Parameter(Mandatory)]
        [ValidateSet('GET', 'POST', 'PUT', 'PATCH', 'DELETE')]
        [string]$Method,

        [Parameter(Mandatory)]
        [int]$ExpectedStatus,

        [Parameter()]
        [string]$Body
    )

    try {
        $requestParameters = @{ UseBasicParsing = $true; Uri = $Uri; Method = $Method }
        if ($PSBoundParameters.ContainsKey('Body')) {
            $requestParameters.Body = $Body
            $requestParameters.ContentType = 'application/json'
        }

        Invoke-WebRequest @requestParameters | Out-Null
        throw "Expected HTTP $ExpectedStatus from $Method $Uri, but the request succeeded."
    }
    catch [Microsoft.PowerShell.Commands.HttpResponseException] {
        $response = $_.Exception.Response
        if ([int]$response.StatusCode -ne $ExpectedStatus) {
            throw "Expected HTTP $ExpectedStatus from $Method $Uri, received $([int]$response.StatusCode)."
        }

        $contentType = $response.Content.Headers.ContentType.MediaType
        if ($contentType -ne 'application/problem+json') {
            throw "Expected application/problem+json from $Method $Uri, received $contentType."
        }

        $problem = $_.ErrorDetails.Message | ConvertFrom-Json
        if ($problem.status -ne $ExpectedStatus -or [string]::IsNullOrWhiteSpace($problem.traceId)) {
            throw "ProblemDetails from $Method $Uri is missing status or traceId."
        }
    }
}

$healthResponse = Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/api/health"
$health = $healthResponse.Content | ConvertFrom-Json

if ($healthResponse.StatusCode -ne 200 -or $health.status -ne 'Healthy') {
    throw 'API health endpoint is not healthy.'
}

$sqlServerCheck = $health.checks | Where-Object name -eq 'sqlserver'
if ($null -eq $sqlServerCheck -or $sqlServerCheck.status -ne 'Healthy') {
    throw 'SQL Server dependency health check is not healthy.'
}

$openApiResponse = Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/openapi/v1.json"
$openApi = $openApiResponse.Content | ConvertFrom-Json
if ($openApiResponse.StatusCode -ne 200 -or $null -eq $openApi.paths.'/api/health') {
    throw 'OpenAPI document does not contain /api/health.'
}

Invoke-ExpectedHttpError -Uri "$BaseUrl/api/not-found" -Method GET -ExpectedStatus 404
Invoke-ExpectedHttpError -Uri "$BaseUrl/api/health" -Method POST -ExpectedStatus 405
Invoke-ExpectedHttpError -Uri "$BaseUrl/api/students/9223372036854775807" -Method GET -ExpectedStatus 404
Invoke-ExpectedHttpError -Uri "$BaseUrl/api/students" -Method POST -ExpectedStatus 400 -Body '{}'

if ($null -eq $openApi.paths.'/api/students/{studentId}' -or $null -eq $openApi.paths.'/api/students') {
    throw 'OpenAPI document does not contain the Students module endpoints.'
}

$corsHeaders = @{
    Origin = 'http://localhost:5173'
    'Access-Control-Request-Method' = 'GET'
}
$corsResponse = Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/api/health" -Method OPTIONS -Headers $corsHeaders
if ($corsResponse.Headers['Access-Control-Allow-Origin'] -ne 'http://localhost:5173') {
    throw 'CORS preflight did not allow the configured frontend origin.'
}

[pscustomobject]@{
    Verification = 'PASS'
    ApiHealth = $health.status
    SqlServerHealth = $sqlServerCheck.status
    OpenApiHealthPath = 'PASS'
    ProblemDetails404 = 'PASS'
    ProblemDetails405 = 'PASS'
    StudentNotFound404 = 'PASS'
    StudentValidation400 = 'PASS'
    OpenApiStudentsPaths = 'PASS'
    CorsPreflight = 'PASS'
}
