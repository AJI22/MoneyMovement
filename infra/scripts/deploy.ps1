# Deploy ARM templates in order. Set parameters in infra/arm/parameters.dev.json.
param(
    [string]$ResourceGroup = "rg-money-movement-poc",
    [string]$Location = "eastus"
)

$ErrorActionPreference = "Stop"
$ArmPath = Join-Path $PSScriptRoot "..\arm"
$ParamsFile = Join-Path $ArmPath "parameters.dev.json"

if (-not (Test-Path $ParamsFile)) {
    Write-Error "Create parameters.dev.json with postgresAdminPassword and other values."
}

Write-Host "Creating resource group $ResourceGroup..."
az group create --name $ResourceGroup --location $Location --output none

Write-Host "Deploying 00-shared (Log Analytics, VNet, Container Apps Environment)..."
az deployment group create --name 00-shared --resource-group $ResourceGroup `
    --template-file (Join-Path $ArmPath "00-shared.json") --parameters $ParamsFile --output none

$out00 = az deployment group show --name 00-shared --resource-group $ResourceGroup --query "properties.outputs" -o json | ConvertFrom-Json
$vnetId = $out00.vnetId.value
$subnetId = $out00.subnetId.value
$containerAppsEnvironmentId = $out00.containerAppsEnvironmentId.value

Write-Host "Deploying 01-data (PostgreSQL)..."
az deployment group create --name 01-data --resource-group $ResourceGroup `
    --template-file (Join-Path $ArmPath "01-data.json") --parameters $ParamsFile --output none

$out01 = az deployment group show --name 01-data --resource-group $ResourceGroup --query "properties.outputs" -o json | ConvertFrom-Json
$postgresFqdn = $out01.postgresFqdn.value

Write-Host "Deploying 02-servicebus..."
az deployment group create --name 02-servicebus --resource-group $ResourceGroup `
    --template-file (Join-Path $ArmPath "02-servicebus.json") --parameters $ParamsFile --output none

Write-Host "Deploying 03-keyvault..."
az deployment group create --name 03-keyvault --resource-group $ResourceGroup `
    --template-file (Join-Path $ArmPath "03-keyvault.json") --parameters $ParamsFile --output none

$out03 = az deployment group show --name 03-keyvault --resource-group $ResourceGroup --query "properties.outputs" -o json | ConvertFrom-Json
$keyVaultUri = $out03.keyVaultUri.value
$userAssignedIdentityId = $out03.userAssignedIdentityId.value
$keyVaultName = $out03.keyVaultName.value

$postgresPassword = az keyvault secret show --vault-name $keyVaultName --name POSTGRES-PASSWORD --query value -o tsv 2>$null
if (-not $postgresPassword) { $postgresPassword = "PLACEHOLDER_CHANGE_ME_Strong_Password!" }

Write-Host "Deploying 05-tigerbeetle-vm..."
az deployment group create --name 05-tigerbeetle-vm --resource-group $ResourceGroup `
    --template-file (Join-Path $ArmPath "05-tigerbeetle-vm.json") --parameters $ParamsFile `
    --parameters vnetId=$vnetId subnetId=$subnetId --output none

$out05 = az deployment group show --name 05-tigerbeetle-vm --resource-group $ResourceGroup --query "properties.outputs" -o json | ConvertFrom-Json
$tigerbeetleEndpoint = $out05.tigerbeetleEndpoint.value

# Build 04 parameters: use a temp file for secure postgresPassword to avoid shell logging
$params04Path = Join-Path $env:TEMP "params-04-containerapps.json"
$baseParams = Get-Content $ParamsFile -Raw | ConvertFrom-Json
$params04Obj = @{
    "$schema" = $baseParams.'$schema'
    contentVersion = $baseParams.contentVersion
    parameters = @{}
}
foreach ($p in $baseParams.parameters.PSObject.Properties) {
    $params04Obj.parameters[$p.Name] = @{ value = $p.Value }
}
$params04Obj.parameters["containerAppsEnvironmentId"] = @{ value = $containerAppsEnvironmentId }
$params04Obj.parameters["keyVaultUri"] = @{ value = $keyVaultUri }
$params04Obj.parameters["userAssignedIdentityId"] = @{ value = $userAssignedIdentityId }
$params04Obj.parameters["postgresFqdn"] = @{ value = $postgresFqdn }
$params04Obj.parameters["tigerbeetleEndpoint"] = @{ value = $tigerbeetleEndpoint }
$params04Obj.parameters["postgresPassword"] = @{ value = $postgresPassword }
# 04 template expects postgresAdmin
$pgAdmin = (Get-Content $ParamsFile -Raw | ConvertFrom-Json).parameters.POSTGRES_ADMIN.value
$params04Obj.parameters["postgresAdmin"] = @{ value = $pgAdmin }
$params04Obj | ConvertTo-Json -Depth 10 | Set-Content $params04Path -Encoding UTF8

Write-Host "Deploying 04-containerapps..."
az deployment group create --name 04-containerapps --resource-group $ResourceGroup `
    --template-file (Join-Path $ArmPath "04-containerapps.json") --parameters $params04Path --output none
Remove-Item $params04Path -ErrorAction SilentlyContinue

Write-Host "Deployment complete."
& (Join-Path $PSScriptRoot "outputs.ps1") -ResourceGroup $ResourceGroup
