param(
    [string]$BaseUrl = "http://127.0.0.1:8080",
    [string]$CommunityServiceUrl = "http://127.0.0.1:5001",
    [string]$InternalToken = $env:INTERNAL_SERVICE_TOKEN
)

if ([string]::IsNullOrWhiteSpace($InternalToken)) {
    $InternalToken = "development-internal-service-token"
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
        -Body ($Body | ConvertTo-Json)
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
$registered = Invoke-JsonPost `
    -Uri "$BaseUrl/api/auth/register" `
    -Body @{
        username = $username
        email = $email
        password = $password
        displayName = "Security Test"
        bio = "Manual smoke test"
    }

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

Assert-HttpFailure `
    -Name "Community list without JWT" `
    -ExpectedStatus 401 `
    -Action { Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/communities" }

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
    -Headers $authHeaders

if ($myCommunities.Count -lt 1) {
    throw "Expected at least one current user community."
}

Write-Host "[ok] Current user communities endpoint returned $($myCommunities.Count) item(s)"

Assert-HttpFailure `
    -Name "Community internal endpoint without internal token" `
    -ExpectedStatus 401 `
    -Action { Invoke-RestMethod -Method Get -Uri "$CommunityServiceUrl/internal/users/$userId/community-ids" }

$communityIds = Invoke-RestMethod `
    -Method Get `
    -Uri "$CommunityServiceUrl/internal/users/$userId/community-ids" `
    -Headers $internalHeaders

Write-Host "[ok] Community internal endpoint accepted internal token and returned $($communityIds.Count) id(s)"

Assert-HttpFailure `
    -Name "Notification inbox without JWT" `
    -ExpectedStatus 401 `
    -Action { Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/notifications" }

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
    -Headers $authHeaders

if ($notifications.items.Count -lt 1) {
    throw "Expected at least one notification for JWT user."
}

Write-Host "[ok] Notification inbox with JWT returned $($notifications.items.Count) item(s)"
Write-Host "Security smoke test completed successfully."
