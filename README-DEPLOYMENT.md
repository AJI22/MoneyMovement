# Money Transfer Platform – Deployment Guide

This document describes how to deploy the money-transfer-platform to **Microsoft Azure** using **Azure DevOps Pipelines** and ARM templates.

---

## Overview of Deployment Architecture

The deployment consists of:

- **ARM templates** (`infra/arm/`): Define all Azure resources in a fixed order.
- **Azure DevOps pipelines** (`infra/pipelines/`): Build, deploy infrastructure, and deploy application images.
- **Scripts** (`infra/scripts/`): Optional local deploy, destroy, build/push images, and smoke tests.

**Resources created:**

| Template | Resources |
|----------|-----------|
| `00-shared.json` | Log Analytics Workspace, Virtual Network, Subnet, Azure Container Apps Environment, optional Application Insights |
| `01-data.json` | Azure PostgreSQL Flexible Server (burstable SKU), databases (ledger, rails_ng, rails_us, fx, orchestrator, temporal) |
| `02-servicebus.json` | Azure Service Bus namespace (Basic tier), queue `events` |
| `03-keyvault.json` | Azure Key Vault, User-Assigned Managed Identity, access policies, placeholder secrets |
| `04-containerapps.json` | Container Apps: ledger-service, rails-nigeria, rails-unitedstates, fx-service, transfer-orchestrator, reconciliation-worker, treasury-worker, temporal-server, temporal-ui |
| `05-tigerbeetle-vm.json` | Linux VM (Standard_B1ms), NSG, cloud-init to install and run TigerBeetle |

Container Apps use the same User-Assigned Identity to read secrets from Key Vault. Application secrets (Postgres password, Service Bus connection, TigerBeetle endpoint, etc.) are stored in Key Vault; Container Apps can reference them or receive values via deployment parameters (as in the current ARM setup).

---

## Azure Prerequisites

Before running pipelines or scripts, ensure you have:

1. **Azure subscription** with permissions to create resource groups, deployments, Container Apps, PostgreSQL, Service Bus, Key Vault, and VMs.
2. **Azure DevOps organization and project** where the repository is hosted.
3. **Azure Container Registry (ACR)** to store container images (create manually or via script; not created by the provided ARM templates).
4. **Service connections** in Azure DevOps:
   - **ARM**: Azure Resource Manager connection (subscription scope) for deploying ARM templates and managing Container Apps.
   - **ACR**: Docker Registry connection to your Azure Container Registry for pushing images from the build pipeline.

---

## Required Pipeline Variables

Configure these in **Pipelines → Library** (variable group) or on each pipeline:

| Variable | Description | Example / Secret |
|----------|-------------|-------------------|
| `AZURE_SERVICE_CONNECTION` | Name of the Azure Resource Manager service connection | `Azure-MoneyMovement` |
| `ACR_SERVICE_CONNECTION` | Name of the Docker Registry (ACR) service connection | `ACR-MoneyMovement` |
| `ACR_LOGIN_SERVER` | ACR login server (no `https://`) | `myregistry.azurecr.io` |

Optional (for **Deploy** pipeline EF migrations): `POSTGRES_CONNECTION_LEDGER`, `POSTGRES_CONNECTION_ORCHESTRATOR` — full connection strings to the ledger and orchestrator databases. If not set, migration steps are skipped (continueOnError).

For **infra** pipeline, subscription/resource group/location can be passed as parameters (see pipeline YAML) or set as variables.

---

## Required Key Vault Secrets

Key Vault (created by `03-keyvault.json`) stores these secrets. Replace placeholders after first deployment:

| Secret Name | Purpose |
|-------------|---------|
| `POSTGRES-PASSWORD` | Password for PostgreSQL admin user |
| `SERVICEBUS-CONNECTION-STRING` | Service Bus namespace connection string |
| `ACR-PASSWORD` | ACR admin password (if using admin auth) |
| `TIGERBEETLE-ENDPOINT` | TigerBeetle endpoint (e.g. `IP:3000`) |
| `TEMPORAL-ENDPOINT` | Temporal server address (e.g. `temporal-server:7233`) |

Container Apps get Postgres and other config via ARM template parameters (e.g. connection strings built from Key Vault values at deploy time). The pipeline that deploys `04-containerapps` reads `POSTGRES-PASSWORD` from Key Vault and passes it into the template.

---

## Deployment Order

Deploy in this order:

1. **Deploy infrastructure**  
   Run the **Infrastructure** pipeline (`azure-pipelines-infra.yml`) or execute `infra/scripts/deploy.ps1`.  
   Order used: `00-shared` → `01-data` → `02-servicebus` → `03-keyvault` → `05-tigerbeetle-vm` → `04-containerapps`.

2. **Build and push images**  
   Run the **Build** pipeline (`azure-pipelines-build.yml`) to build the solution, run tests, build Docker images, and push them to ACR.  
   Alternatively, run `infra/scripts/build-images.ps1` and `infra/scripts/push-images.ps1` locally (after `az acr login`).

3. **Deploy application**  
   Run the **Deploy** pipeline (`azure-pipelines-deploy.yml`) to update Container App images to the latest from ACR, run database migrations (if configured), restart apps, and run smoke tests.

---

## Configuring Azure DevOps Service Connections

### Azure Resource Manager (ARM)

1. In Azure DevOps: **Project Settings** → **Service connections** → **New service connection** → **Azure Resource Manager**.
2. Choose **Service principal (automatic)** or **Service principal (manual)**.
3. Select the subscription and resource group (or entire subscription).
4. Name the connection (e.g. `Azure-MoneyMovement`) and set the pipeline variable `AZURE_SERVICE_CONNECTION` to this name.

### Azure Container Registry (ACR)

1. **New service connection** → **Docker Registry**.
2. Choose **Azure Container Registry** and select your subscription and registry.
3. Name the connection (e.g. `ACR-MoneyMovement`) and set `ACR_SERVICE_CONNECTION` to this name.
4. Set `ACR_LOGIN_SERVER` to your ACR login server (e.g. `myregistry.azurecr.io`).

---

## Pushing Container Images

- **Via pipeline**: The build pipeline builds and pushes images to ACR using `ACR_SERVICE_CONNECTION` and `ACR_LOGIN_SERVER`. No manual push needed if the build runs on commit.
- **Manually**:
  1. `az acr login --name <registry-name>`
  2. From repo root: `.\infra\scripts\build-images.ps1 -Tag latest`
  3. `$env:ACR_LOGIN_SERVER = "myregistry.azurecr.io"; .\infra\scripts\push-images.ps1 -Tag latest`

---

## Running Smoke Tests

- **Via pipeline**: The Deploy pipeline runs a smoke test against the orchestrator health endpoint after updating images.
- **Locally** (PowerShell):
  - Option 1: Set orchestrator URL and run  
    `.\infra\scripts\smoke-tests.ps1 -OrchestratorBaseUrl "https://ca-orchestrator-dev.azurecontainerapps.io"`
  - Option 2: Use Azure CLI to resolve FQDN:  
    `.\infra\scripts\smoke-tests.ps1 -ResourceGroup rg-money-movement-poc -EnvironmentName dev`

Smoke test validates `GET /healthz` on the transfer-orchestrator.

---

## Placeholder Values to Replace

Before production (or a real POC), replace these in `infra/arm/parameters.dev.json` and/or Key Vault:

| Placeholder | Where | Description |
|-------------|--------|-------------|
| Subscription ID | Pipeline parameters / scripts | Your Azure subscription ID |
| Resource group name | `resourceGroup` | e.g. `rg-money-movement-poc` |
| Container registry name / ACR login server | `acrLoginServer` / pipeline variables | Your ACR URL |
| Postgres password | `POSTGRES_PASSWORD` / Key Vault `POSTGRES-PASSWORD` | Strong admin password |
| Service Bus connection string | Key Vault `SERVICEBUS-CONNECTION-STRING` | From Service Bus namespace |
| Key Vault name | `keyVaultName` | Must be globally unique |
| TigerBeetle endpoint | Key Vault / `05-tigerbeetle-vm` output | e.g. VM private IP and port 3000 |
| Azure DevOps principal ID | `azureDevOpsPrincipalId` | Object ID of the service principal used by the pipeline (for Key Vault access) |

---

## Troubleshooting

### Infrastructure deployment fails

- **PostgreSQL or Service Bus name already exists**: Use a unique `serviceBusNamespaceName` and ensure PostgreSQL server name is unique within Azure.
- **Key Vault name not unique**: Change `keyVaultName` in parameters to a globally unique value.
- **Deployment dependency / output missing**: Ensure deployments are run in order and use the same deployment names (`00-shared`, `01-data`, etc.) so `az deployment group show` can retrieve outputs.

### Build pipeline fails

- **ACR_SERVICE_CONNECTION or ACR_LOGIN_SERVER not set**: Add pipeline variables (or variable group) and ensure the ACR service connection exists.
- **Code coverage below 80%**: Fix or add unit tests, or temporarily lower the threshold in the pipeline variable `coverageThreshold` (or adjust the verification step).

### Deploy pipeline fails

- **Container App not found**: Ensure infra pipeline (or deploy script) has created Container Apps with names like `ca-ledger-dev`, `ca-orchestrator-dev`, etc., and that `resourceGroup` and `environmentName` parameters match.
- **Image pull error**: Ensure ACR is connected to the Container Apps environment (ACR credentials or managed identity) and that the image tag exists in ACR.

### Smoke test fails

- **Orchestrator not reachable**: Confirm the orchestrator Container App has external ingress and is running; check FQDN in Azure Portal or with `az containerapp show ... --query properties.configuration.ingress.fqdn`.
- **SSL/certificate errors**: Container Apps use HTTPS; ensure you call `https://` and that no proxy strips certificates.

### Key Vault access denied

- **Pipeline**: Grant the Azure DevOps service principal (used by `AZURE_SERVICE_CONNECTION`) **Key Vault Secrets User** (or appropriate role) on the Key Vault, or add an access policy for that principal's object ID (`azureDevOpsPrincipalId` in parameters).
- **Container Apps**: The User-Assigned Managed Identity used by Container Apps must have Key Vault access (e.g. "Get" and "List" on secrets). This is configured in `03-keyvault.json` for the created identity.

---

## Destroying Resources

To delete all resources in the resource group:

- **PowerShell**: `.\infra\scripts\destroy.ps1 -ResourceGroup rg-money-movement-poc`
- **Azure CLI**: `az group delete --name rg-money-movement-poc --yes --no-wait`

This does not delete the Azure Container Registry if it was created separately.
