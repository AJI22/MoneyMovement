# Push Docker images to Azure Container Registry. Run after build-images.ps1.
# Set env: ACR_LOGIN_SERVER (e.g. myregistry.azurecr.io), or pass -AcrLoginServer.
# Authenticate first: az acr login --name <registry-name>
param(
    [string]$Tag = "latest",
    [string]$AcrLoginServer = $env:ACR_LOGIN_SERVER
)

$ErrorActionPreference = "Stop"
if (-not $AcrLoginServer) { Write-Error "Set ACR_LOGIN_SERVER env or pass -AcrLoginServer (e.g. myregistry.azurecr.io)" }

$images = @(
    "ledger-service", "rails-nigeria", "rails-unitedstates", "fx-service",
    "transfer-orchestrator", "reconciliation-worker", "treasury-worker"
)

foreach ($name in $images) {
    $local = "${name}:${Tag}"
    $remote = "${AcrLoginServer}/${name}:${Tag}"
    Write-Host "Tagging and pushing $remote..."
    docker tag $local $remote 2>$null
    if ($LASTEXITCODE -ne 0) { Write-Warning "Image $local not found; run build-images.ps1 first."; continue }
    docker push $remote
    if ($LASTEXITCODE -ne 0) { throw "Push failed for $remote" }
}
Write-Host "All images pushed to $AcrLoginServer with tag $Tag"
