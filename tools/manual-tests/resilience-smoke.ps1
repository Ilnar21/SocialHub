param(
    [string]$BaseUrl = "http://127.0.0.1:8080",
    [string]$NotificationContainer = ""
)

$ErrorActionPreference = "Stop"

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

function Resolve-NotificationContainer {
    param([string]$PreferredName)

    if (![string]::IsNullOrWhiteSpace($PreferredName)) {
        $preferredId = docker ps -a --filter "name=$PreferredName" --format "{{.Names}}" | Select-Object -First 1
        if (![string]::IsNullOrWhiteSpace($preferredId)) {
            return $preferredId
        }
    }

    return docker ps -a --filter "name=notification-service" --format "{{.Names}}" | Select-Object -First 1
}

$suffix = Get-Random -Minimum 100000 -Maximum 999999
$ownerUsername = "resilience_owner_$suffix"
$memberUsername = "resilience_member_$suffix"
$password = "Password123!"

Write-Host "Registering owner and member users"
$ownerRegister = Invoke-JsonPost `
    -Uri "$BaseUrl/api/auth/register" `
    -Body @{
        username = $ownerUsername
        email = "$ownerUsername@example.com"
        password = $password
        displayName = "Resilience Owner"
        bio = "Manual resilience test"
    }

$memberRegister = Invoke-JsonPost `
    -Uri "$BaseUrl/api/auth/register" `
    -Body @{
        username = $memberUsername
        email = "$memberUsername@example.com"
        password = $password
        displayName = "Resilience Member"
        bio = "Manual resilience test"
    }

$ownerLogin = Invoke-JsonPost `
    -Uri "$BaseUrl/api/auth/login" `
    -Body @{
        usernameOrEmail = $ownerUsername
        password = $password
    }

$memberLogin = Invoke-JsonPost `
    -Uri "$BaseUrl/api/auth/login" `
    -Body @{
        usernameOrEmail = $memberUsername
        password = $password
    }

$ownerHeaders = @{ Authorization = "Bearer $($ownerLogin.token)" }
$memberHeaders = @{ Authorization = "Bearer $($memberLogin.token)" }

$community = Invoke-JsonPost `
    -Uri "$BaseUrl/api/communities" `
    -Headers $ownerHeaders `
    -Body @{
        name = "Resilience Community $suffix"
        description = "Notification failure should not break suggested posts"
        type = "Open"
    }

Invoke-JsonPost `
    -Uri "$BaseUrl/api/communities/$($community.id)/join" `
    -Headers $memberHeaders `
    -Body @{} | Out-Null

$stoppedNotification = $false
$resolvedNotificationContainer = Resolve-NotificationContainer $NotificationContainer

try {
    if (![string]::IsNullOrWhiteSpace($resolvedNotificationContainer)) {
        Write-Host "Stopping $resolvedNotificationContainer to simulate NotificationService failure"
        docker stop $resolvedNotificationContainer | Out-Null
        $stoppedNotification = $true
        Start-Sleep -Seconds 2
    }
    else {
        Write-Host "NotificationService container was not found. Continuing without docker stop."
    }

    $suggested = Invoke-JsonPost `
        -Uri "$BaseUrl/api/communities/$($community.id)/suggested-posts" `
        -Headers $memberHeaders `
        -Body @{
            title = "Suggested while notification is down"
            text = "CommunityService must save this even if NotificationService is unavailable."
        }

    if ($suggested.status -ne "Pending") {
        throw "Expected suggested post status Pending, got $($suggested.status)."
    }

    Write-Host "[ok] Suggested post was saved while NotificationService was unavailable: $($suggested.id)"
}
finally {
    if ($stoppedNotification) {
        Write-Host "Starting $resolvedNotificationContainer again"
        docker start $resolvedNotificationContainer | Out-Null
    }
}

Write-Host "Resilience smoke test completed successfully."
