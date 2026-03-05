# Architecture

## Single ledger truth

Only **Ledger.Service** holds and computes financial balances. It is the only service that talks to TigerBeetle. All other services:

- Call Ledger HTTP API to **record** events they cause (postings, reserves, releases).
- Never compute or store balances themselves.

Examples:

- **Rails.Nigeria**: When NGN is confirmed, it calls Ledger to post `NGN_BANK` (debit) and `CUSTOMER_FUNDS_NGN` (credit).
- **Rails.UnitedStates**: When a payout is sent, it posts `USD_PAYOUT_RESERVE` (debit) and `USD_BANK` (credit).
- **FX.Service**: On execute, it posts customer funds → FX pool NGN, then FX pool NGN → FX pool USD.
- **Transfer.Orchestrator**: Calls Ledger to reserve FX_POOL_USD → USD_PAYOUT_RESERVE before requesting payout.

## Customer funds as liabilities

`CUSTOMER_FUNDS_NGN` (and similar) are **liability** accounts: we owe that value to the customer. In TigerBeetle they use `CreditsMustNotExceedDebits` (or the appropriate constraint) so that we cannot credit more than we have received. This keeps the books consistent and prevents over-issuing customer balances.

## Routing inside country rails

The orchestrator never chooses a specific vendor (Paystack, Flutterwave, ACH, etc.). It only says “collect NGN for this transfer” or “send USD payout for this transfer”. Each **country rail service** owns:

- Provider/vendor selection
- Health and circuit breaker state
- Idempotency and retries

So Rails.Nigeria decides Paystack vs Flutterwave internally; Rails.UnitedStates decides ACH vs FedNow internally.

## Why no Redis

This POC uses no Redis. State is in PostgreSQL (operational and outbox) and TigerBeetle (financial). For POC scale, in-memory bus and single-instance services are sufficient. Redis can be introduced later for caching or pub/sub if needed.

## Idempotency end-to-end

- Every POST that causes a side effect accepts **Idempotency-Key** (and optionally **X-Correlation-ID**).
- Rails and FX store idempotency keys and return the same response for duplicate keys.
- Ledger stores (TransferId, OperationType, IdempotencyKey) and rejects or replays as appropriate.
- This prevents double postings and double payouts on retries.
