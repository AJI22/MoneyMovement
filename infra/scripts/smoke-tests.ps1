# Smoke tests: call platform health endpoints. Set OrchestratorBaseUrl or pass -OrchestratorBaseUrl.
# Example: .\smoke-tests.ps1 -OrchestratorBaseUrl "https://ca-orchestrator-dev.azurecontainerapps.io"
param(
    [string]$OrchestratorBaseUrl = $env:OrchestratorBaseUrl,
    [string]$ResourceGroup = "rg-money-movement-poc",
    [string]$EnvironmentName = "dev"
)

$ErrorActionPreference = "Stop"

if (-not $OrchestratorBaseUrl) {
    Write-Host "Resolving orchestrator FQDN from Azure..."
    $fqdn = az containerapp show --name "ca-orchestrator-$EnvironmentName" --resource-group $ResourceGroup --query "properties.configuration.ingress.fqdn" -o tsv 2>$null
    if (-not $fqdn) { Write-Error "Could not get orchestrator FQDN. Set OrchestratorBaseUrl or ensure ResourceGroup/EnvironmentName are correct." }
    $OrchestratorBaseUrl = "https://$fqdn"
}

$OrchestratorBaseUrl = $OrchestratorBaseUrl.TrimEnd('/')
$healthUrl = "$OrchestratorBaseUrl/healthz"

Write-Host "Smoke test: GET $healthUrl"
try {
    $r = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 30
    if ($r.StatusCode -ge 200 -and $r.StatusCode -lt 300) {
        Write-Host "PASS: Orchestrator health returned $($r.StatusCode)"
    } else {
        Write-Warning "Unexpected status: $($r.StatusCode)"
        exit 1
    }
} catch {
    Write-Error "Smoke test failed: $_"
    exit 1
}

Write-Host "Smoke tests completed successfully."
