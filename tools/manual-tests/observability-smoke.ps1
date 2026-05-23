param(
    [string]$BaseUrl = "http://127.0.0.1:8080",
    [string]$PrometheusUrl = "http://127.0.0.1:9090"
)

$ErrorActionPreference = "Stop"

Write-Host "Checking frontend metrics through gateway"
$metrics = Invoke-RestMethod -Method Get -Uri "$BaseUrl/metrics" -ErrorAction Stop
if ($metrics -notmatch "http_requests_received_total") {
    throw "Gateway /metrics did not return prometheus-net HTTP counters."
}
Write-Host "[ok] Gateway exposes frontend metrics"

Write-Host "Checking Prometheus readiness"
$ready = Invoke-WebRequest -Method Get -Uri "$PrometheusUrl/-/ready" -UseBasicParsing -ErrorAction Stop
if ($ready.StatusCode -ne 200) {
    throw "Prometheus readiness returned HTTP $($ready.StatusCode)."
}
Write-Host "[ok] Prometheus is ready"

Write-Host "Checking Prometheus targets"
$targets = Invoke-RestMethod -Method Get -Uri "$PrometheusUrl/api/v1/targets" -ErrorAction Stop
$activeTargets = @($targets.data.activeTargets)
if ($activeTargets.Count -lt 8) {
    throw "Expected at least 8 active scrape targets, got $($activeTargets.Count)."
}

$unhealthyTargets = @($activeTargets | Where-Object { $_.health -ne "up" })
if ($unhealthyTargets.Count -gt 0) {
    $labels = ($unhealthyTargets | ForEach-Object { $_.labels.instance }) -join ", "
    throw "Prometheus has unhealthy targets: $labels"
}

Write-Host "[ok] Prometheus scrapes $($activeTargets.Count) healthy target(s)"
Write-Host "Observability smoke test completed successfully."
