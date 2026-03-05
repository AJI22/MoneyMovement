# Smoke test: health and sample transfer. Set BaseUrl if not local.
param([string]$OrchestratorBaseUrl = "http://localhost:5084")

$ErrorActionPreference = "Stop"
Write-Host "Health check..."
$health = Invoke-RestMethod -Uri "$OrchestratorBaseUrl/healthz" -Method Get
Write-Host "Health: $health"

Write-Host "Creating transfer..."
$body = '{"userId":"smoke-user","recipientId":"smoke-recipient","sourceCurrency":"NGN","sourceAmount":10000,"destinationCurrency":"USD"}'
$create = Invoke-RestMethod -Uri "$OrchestratorBaseUrl/transfers" -Method Post -Body $body -ContentType "application/json"
Write-Host "Transfer created: $($create.transferId), status: $($create.status)"
$tid = $create.transferId
Start-Sleep -Seconds 2
$get = Invoke-RestMethod -Uri "$OrchestratorBaseUrl/transfers/$tid" -Method Get
Write-Host "Transfer status: $($get.status)"
Write-Host "Smoke test done."
