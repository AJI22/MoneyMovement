# Build Docker images for all platform services. Run from repository root.
param([string]$Tag = "latest")

$ErrorActionPreference = "Stop"
$Root = if ($PSScriptRoot) { (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path } else { (Get-Location).Path }

$services = @(
    @{ Name = "ledger-service"; Dockerfile = "src\Ledger.Service\Dockerfile" },
    @{ Name = "rails-nigeria"; Dockerfile = "src\Rails.Nigeria\Dockerfile" },
    @{ Name = "rails-unitedstates"; Dockerfile = "src\Rails.UnitedStates\Dockerfile" },
    @{ Name = "fx-service"; Dockerfile = "src\FX.Service\Dockerfile" },
    @{ Name = "transfer-orchestrator"; Dockerfile = "src\Transfer.Orchestrator\Dockerfile" },
    @{ Name = "reconciliation-worker"; Dockerfile = "src\Reconciliation.Worker\Dockerfile" },
    @{ Name = "treasury-worker"; Dockerfile = "src\Treasury.Worker\Dockerfile" }
)

Push-Location $Root
try {
    foreach ($svc in $services) {
        $df = Join-Path $Root $svc.Dockerfile
        if (-not (Test-Path $df)) { Write-Warning "Dockerfile not found: $df"; continue }
        Write-Host "Building $($svc.Name):$Tag..."
        docker build -f $df -t "$($svc.Name):$Tag" .
        if ($LASTEXITCODE -ne 0) { throw "Build failed for $($svc.Name)" }
    }
    Write-Host "All images built with tag $Tag"
} finally {
    Pop-Location
}
