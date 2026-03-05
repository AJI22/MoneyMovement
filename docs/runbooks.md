# Runbooks

## Debug with correlation ID

1. Get `X-Correlation-ID` from the response or client request.
2. Search logs (e.g. Log Analytics in Azure, or local logs) for that value.
3. Trace the same ID across Orchestrator → Rails → FX → Ledger to see where a step failed or timed out.

## Re-run a workflow

- In this POC, the flow runs in-process (no Temporal). To retry a stuck transfer:
  - If status is ManualReview or Failed, fix the underlying issue (e.g. liquidity, provider).
  - Optionally implement a “retry” endpoint that re-invokes the flow from the last failed step (or from reserve/payout) with the same idempotency keys where applicable so no double posting occurs.

## Inspect ledger state

- Call `GET /ledger/balances/FX_POOL_USD` (and other accounts) on Ledger.Service.
- Balances are returned in the configured currency; TigerBeetle stores integer units (see Ledger.Service scale).

## Interpret reconciliation alerts

- Reconciliation Worker compares ledger balances to stub “external” data. A mismatch log means the stub and ledger differ (expected in tests when you force a discrepancy). In production, external data would be real bank/partner statements.
