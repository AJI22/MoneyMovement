# Money Movement Platform (POC)

Internal cross-border money transfer infrastructure. Not customer-facing; orchestrates transfers, country rails, FX, and a single TigerBeetle-backed ledger.

## Architecture

- **Transfer.Orchestrator**: Creates transfers and runs the transfer flow (collect NGN → FX → reserve USD → payout USD).
- **Rails.Nigeria**: NGN collection; routes internally across stub providers (AlwaysSuccess, FlakyDown).
- **Rails.UnitedStates**: USD payout; same pattern with stub providers.
- **FX.Service**: Quote, accept, execute; stub venues (AlwaysSuccess, FlakyDown, PartialFill).
- **Ledger.Service**: Single source of truth; TigerBeetle double-entry; posting, reserve, release, balances.
- **Reconciliation.Worker**: Compares ledger vs stub external data.
- **Treasury.Worker**: Monitors FX_POOL_USD and logs liquidity low.

All financial state is in the ledger; no service computes balances.

## Local Run

1. **Start stack**

   ```powershell
   .\run-local.ps1
   ```

   Or manually:

   ```powershell
   docker compose up -d postgres tigerbeetle
   # Create DBs: ledger, rails_ng, rails_us, fx, orchestrator
   dotnet ef database update --project src/Ledger.Service
   # ... same for Rails.Nigeria, Rails.UnitedStates, FX.Service, Transfer.Orchestrator
   docker compose up -d
   curl -X POST http://localhost:5080/ledger/bootstrap
   ```

2. **Sample transfer**

   ```powershell
   curl -X POST http://localhost:5084/transfers -H "Content-Type: application/json" -d '{"userId":"u1","recipientId":"r1","sourceCurrency":"NGN","sourceAmount":100000,"destinationCurrency":"USD"}'
   # Returns transferId and status Created; flow runs in background.
   curl http://localhost:5084/transfers/{transferId}
   ```

3. **Swagger**

   - Orchestrator: http://localhost:5084/swagger  
   - Ledger: http://localhost:5080/swagger  
   - Rails Nigeria: http://localhost:5081/swagger  
   - Rails US: http://localhost:5082/swagger  
   - FX: http://localhost:5083/swagger  

## Simulate provider down

- **Rails.Nigeria**: Set `SimulateNgProviderDown=true` and optionally `SimulateTimeoutRate=0.9`.
- **Rails.UnitedStates**: Set `SimulateUsProviderDown=true`.
- **FX**: Set `SimulateFxPartialFill=true` or `SimulateTimeoutRate=0.5`.

(In Docker, set in `environment` in docker-compose for the relevant service.)

## Tests

```powershell
.\run-tests.ps1
```

Or: `dotnet test tests/UnitTests` and `dotnet test tests/IntegrationTests`.

## Deploy to Azure

See `infra/scripts/deploy.ps1`. Uses ARM templates under `infra/arm` (00-shared, 01-data, 02-servicebus, 03-containerapps, 04-tigerbeetle-vm). Parameters in `parameters.dev.json`.

## Cost estimate (POC &lt; $50/month)

- PostgreSQL Flexible Server (burstable, smallest): ~$15–25  
- Service Bus (Basic/minimal Standard): ~$5–15  
- TigerBeetle VM (B1s/B1ms): ~$7–12  
- Log Analytics (7-day retention, minimal): ~$3–10  
- Container Apps (Consumption, scale-to-zero): often $0–5 with free grant  

Exact figures depend on region and usage. Keep min replicas at 0 where possible and logs minimal.

## Docs

- [Architecture](docs/architecture.md)  
- [Diagrams](docs/diagrams.md)  
- [Failure modes](docs/failure-modes.md)  
- [Runbooks](docs/runbooks.md)  
- [API contracts](docs/api-contracts.md)  

## Adding real vendors

- **Nigeria**: Implement `INigeriaPaymentProvider` in Rails.Nigeria (e.g. PaystackAdapter, FlutterwaveAdapter); orchestrator does not choose providers.
- **US**: Implement `IUnitedStatesPayoutProvider` in Rails.UnitedStates (e.g. ACHAdapter, FedNowAdapter).
- **FX**: Implement `IFxVenue` in FX.Service (e.g. CryptoExchangeAdapter, OTCDeskAdapter).
