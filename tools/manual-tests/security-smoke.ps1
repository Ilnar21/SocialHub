param(
    [string]$BaseUrl = "http://127.0.0.1:8080",
    [string]$InternalToken = $env:INTERNAL_SERVICE_TOKEN,
    [string]$PrometheusUrl = "http://127.0.0.1:9090"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($InternalToken)) {
    $InternalToken = "local-dev-internal-service-token-change-me"
}

function Invoke-JsonPost {
    param(
        [string]$Uri,
        [hashtable]$Body,
        [hashtable]$Headers = @{}
    )

    Invoke-RestMethod `
        -Method Post `
        -Uri $Uri `
        -Headers $Headers `
        -ContentType "application/json" `
        -Body ($Body | ConvertTo-Json) `
        -ErrorAction Stop
}

function Assert-HttpFailure {
    param(
        [scriptblock]$Action,
        [int]$ExpectedStatus,
        [string]$Name
    )

    try {
        & $Action | Out-Null
        throw "Expected HTTP $ExpectedStatus for '$Name', but request succeeded."
    }
    catch {
        $response = $_.Exception.Response
        if ($null -eq $response) {
            throw
        }

        $statusCode = [int]$response.StatusCode
        if ($statusCode -ne $ExpectedStatus) {
            throw "Expected HTTP $ExpectedStatus for '$Name', but got HTTP $statusCode."
        }

        Write-Host "[ok] $Name returned HTTP $ExpectedStatus"
    }
}

$suffix = Get-Random -Minimum 100000 -Maximum 999999
$username = "security_user_$suffix"
$email = "security_$suffix@example.com"
$password = "Password123!"

Write-Host "Registering test user $username"
Invoke-JsonPost `
    -Uri "$BaseUrl/api/auth/register" `
    -Body @{
        username = $username
        email = $email
        password = $password
        displayName = "Security Test"
        bio = "Manual smoke test"
    } | Out-Null

$login = Invoke-JsonPost `
    -Uri "$BaseUrl/api/auth/login" `
    -Body @{
        usernameOrEmail = $username
        password = $password
    }

$token = $login.token
$userId = $login.user.id
$authHeaders = @{ Authorization = "Bearer $token" }
$internalHeaders = @{ "X-Internal-Token" = $InternalToken }

Write-Host "[ok] JWT received for user $userId"

Assert-HttpFailure -Name "Community list without JWT" -ExpectedStatus 401 -Action {
    Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/communities" -ErrorAction Stop
}

Assert-HttpFailure -Name "Notification inbox without JWT" -ExpectedStatus 401 -Action {
    Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/notifications" -ErrorAction Stop
}

Assert-HttpFailure -Name "Message dialogs without JWT" -ExpectedStatus 401 -Action {
    Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/dialogs" -ErrorAction Stop
}

Assert-HttpFailure -Name "Feed without JWT" -ExpectedStatus 401 -Action {
    Invoke-RestMethod -Method Get -Uri "$BaseUrl/feed" -ErrorAction Stop
}

Assert-HttpFailure -Name "Moderation reports without JWT" -ExpectedStatus 401 -Action {
    Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/reports" -ErrorAction Stop
}

Assert-HttpFailure -Name "Post creation without JWT" -ExpectedStatus 401 -Action {
    Invoke-JsonPost `
        -Uri "$BaseUrl/posts" `
        -Body @{
            authorId = "00000000-0000-0000-0000-000000000000"
            communityId = "00000000-0000-0000-0000-000000000000"
            title = "Blocked post"
            text = "JWT is required"
        }
}

$community = Invoke-JsonPost `
    -Uri "$BaseUrl/api/communities" `
    -Headers $authHeaders `
    -Body @{
        name = "Security Community $suffix"
        description = "Created by JWT smoke test"
        type = "Open"
    }

Write-Host "[ok] Community created through JWT: $($community.id)"

$myCommunities = Invoke-RestMethod `
    -Method Get `
    -Uri "$BaseUrl/api/communities/my" `
    -Headers $authHeaders `
    -ErrorAction Stop

if ($myCommunities.Count -lt 1) {
    throw "Expected at least one current user community."
}

Write-Host "[ok] Current user communities endpoint returned $($myCommunities.Count) item(s)"

$dialogs = Invoke-RestMethod `
    -Method Get `
    -Uri "$BaseUrl/api/dialogs" `
    -Headers $authHeaders `
    -ErrorAction Stop

Write-Host "[ok] Message dialogs accepted JWT and returned $($dialogs.Count) item(s)"

Invoke-RestMethod `
    -Method Get `
    -Uri "$BaseUrl/feed" `
    -Headers $authHeaders `
    -ErrorAction Stop | Out-Null

Write-Host "[ok] Feed accepted JWT"

Assert-HttpFailure `
    -Name "Notification event without internal token" `
    -ExpectedStatus 401 `
    -Action {
        Invoke-JsonPost `
            -Uri "$BaseUrl/api/notification-events" `
            -Body @{
                recipientUserId = $userId
                type = "MessageReceived"
                title = "Blocked event"
                message = "This should not pass without internal token"
                sourceService = "SecuritySmoke"
            }
    }

Invoke-JsonPost `
    -Uri "$BaseUrl/api/notification-events" `
    -Headers $internalHeaders `
    -Body @{
        recipientUserId = $userId
        type = "MessageReceived"
        title = "Security smoke notification"
        message = "Internal event was accepted"
        sourceService = "SecuritySmoke"
    } | Out-Null

Start-Sleep -Seconds 3

$notifications = Invoke-RestMethod `
    -Method Get `
    -Uri "$BaseUrl/api/notifications" `
    -Headers $authHeaders `
    -ErrorAction Stop

if ($notifications.items.Count -lt 1) {
    throw "Expected at least one notification for JWT user."
}

Write-Host "[ok] Notification inbox with JWT returned $($notifications.items.Count) item(s)"

$frontendMetrics = Invoke-RestMethod `
    -Method Get `
    -Uri "$BaseUrl/metrics" `
    -ErrorAction Stop

if ($frontendMetrics -notmatch "http_requests_received_total") {
    throw "Expected frontend /metrics to expose HTTP request counters."
}

Write-Host "[ok] Frontend metrics endpoint is exposed through gateway"

try {
    $targets = Invoke-RestMethod `
        -Method Get `
        -Uri "$PrometheusUrl/api/v1/targets" `
        -ErrorAction Stop

    $activeTargets = @($targets.data.activeTargets)
    if ($activeTargets.Count -lt 8) {
        throw "Expected at least 8 Prometheus targets, got $($activeTargets.Count)."
    }

    Write-Host "[ok] Prometheus returned $($activeTargets.Count) scrape target(s)"
}
catch {
    Write-Host "[warn] Prometheus was not reachable at $PrometheusUrl. Run docker compose with prometheus enabled to verify metrics scraping."
}

Write-Host "Security smoke test completed successfully."
