param([string]$ResourceGroup = "rg-money-movement-poc")

$ErrorActionPreference = "Stop"
Write-Host "Resource Group: $ResourceGroup"
$group = az group show --name $ResourceGroup --query "id" -o tsv 2>$null
if (-not $group) { Write-Host "Resource group not found."; exit 1 }
Write-Host "PostgreSQL: use connection string from Azure Portal (Flexible Server)."
Write-Host "Service Bus: use connection string from namespace."
Write-Host "Container Apps: FQDN from each app in Container Apps Environment."
