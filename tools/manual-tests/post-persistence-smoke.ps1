param(
    [string]$BaseUrl = "http://127.0.0.1:8080",
    [string]$PostContainer = ""
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

function Resolve-PostContainer {
    param([string]$PreferredName)

    if (![string]::IsNullOrWhiteSpace($PreferredName)) {
        $preferredId = docker ps -a --filter "name=$PreferredName" --format "{{.Names}}" | Select-Object -First 1
        if (![string]::IsNullOrWhiteSpace($preferredId)) {
            return $preferredId
        }
    }

    return docker ps -a --filter "name=post-service" --format "{{.Names}}" | Select-Object -First 1
}

$suffix = Get-Random -Minimum 100000 -Maximum 999999
$username = "post_persist_$suffix"
$password = "Password123!"
$postText = "Post content persisted through PostgreSQL metadata and MinIO object storage $suffix"

Write-Host "Registering post persistence user $username"
Invoke-JsonPost `
    -Uri "$BaseUrl/api/auth/register" `
    -Body @{
        username = $username
        email = "$username@example.com"
        password = $password
        displayName = "Post Persistence"
        bio = "Manual integration test"
    } | Out-Null

$login = Invoke-JsonPost `
    -Uri "$BaseUrl/api/auth/login" `
    -Body @{
        usernameOrEmail = $username
        password = $password
    }

$headers = @{ Authorization = "Bearer $($login.token)" }

$community = Invoke-JsonPost `
    -Uri "$BaseUrl/api/communities" `
    -Headers $headers `
    -Body @{
        name = "Post Persistence Community $suffix"
        description = "PostService persistence smoke test"
        type = "Open"
    }

$post = Invoke-JsonPost `
    -Uri "$BaseUrl/posts" `
    -Headers $headers `
    -Body @{
        authorId = "00000000-0000-0000-0000-000000000000"
        communityId = $community.id
        title = "Persistent post $suffix"
        text = $postText
    }

Write-Host "[ok] Post created: $($post.id)"

$beforeRestart = Invoke-RestMethod `
    -Method Get `
    -Uri "$BaseUrl/posts/$($post.id)" `
    -Headers $headers `
    -ErrorAction Stop

if ($beforeRestart.text -ne $postText) {
    throw "Expected post text before restart to match persisted content."
}

$resolvedPostContainer = Resolve-PostContainer $PostContainer
if ([string]::IsNullOrWhiteSpace($resolvedPostContainer)) {
    throw "PostService container was not found. Start docker compose first or pass -PostContainer."
}

Write-Host "Restarting $resolvedPostContainer to verify PostgreSQL/MinIO persistence"
docker restart $resolvedPostContainer | Out-Null
Start-Sleep -Seconds 5

$afterRestart = Invoke-RestMethod `
    -Method Get `
    -Uri "$BaseUrl/posts/$($post.id)" `
    -Headers $headers `
    -ErrorAction Stop

if ($afterRestart.id -ne $post.id -or $afterRestart.text -ne $postText) {
    throw "Expected post to be readable with the same content after PostService restart."
}

Write-Host "[ok] Post remained readable after restart: $($afterRestart.id)"
Write-Host "Post persistence smoke test completed successfully."
