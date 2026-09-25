function Get-LocalEnvironmentValue {
    param([Parameter(Mandatory)][string]$Name)

    $repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    $environmentFile = Join-Path $repositoryRoot 'infrastructure\.env'
    if (-not (Test-Path -LiteralPath $environmentFile)) {
        throw 'Missing infrastructure/.env. Copy infrastructure/.env.example and provide local secret values.'
    }

    $line = Get-Content -LiteralPath $environmentFile |
        Where-Object { $_ -match "^$([regex]::Escape($Name))=" } |
        Select-Object -Last 1
    if ([string]::IsNullOrWhiteSpace($line)) {
        throw "Missing $Name in infrastructure/.env."
    }

    return ($line -split '=', 2)[1].Trim()
}

function Get-AdminAuthToken {
    param([Parameter(Mandatory)][string]$BaseUrl)

    $email = Get-LocalEnvironmentValue 'AUTH_BOOTSTRAP_ADMIN_EMAIL'
    $password = Get-LocalEnvironmentValue 'AUTH_BOOTSTRAP_ADMIN_PASSWORD'
    $body = @{ email = $email; password = $password } | ConvertTo-Json
    $response = Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/api/auth/login" -Method POST `
        -ContentType 'application/json' -Body $body
    $token = ($response.Content | ConvertFrom-Json).accessToken
    if ([string]::IsNullOrWhiteSpace($token)) { throw 'Admin login did not return an access token.' }
    return $token
}

function Merge-AuthorizationHeaders {
    param([Parameter()][hashtable]$Headers)

    $result = @{ Authorization = "Bearer $script:AccessToken" }
    if ($null -ne $Headers) {
        foreach ($key in $Headers.Keys) { $result[$key] = $Headers[$key] }
    }
    return $result
}
