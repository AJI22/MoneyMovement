param([string]$ResourceGroup = "rg-money-movement-poc")
$ErrorActionPreference = "Stop"
Write-Host "Deleting resource group $ResourceGroup..."
az group delete --name $ResourceGroup --yes --no-wait
