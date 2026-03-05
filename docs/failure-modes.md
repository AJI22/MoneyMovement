# Failure modes and system response

| Failure | System response |
|--------|------------------|
| Nigeria provider down | Rails.Nigeria tries next healthy provider; circuit breaker marks provider unhealthy after threshold failures. |
| US provider down | Same: fallback to next provider; circuit breaker. |
| FX quote expired | Accept returns error; orchestrator can re-quote up to N times, then ManualReview. |
| FX execution failure | Transfer set to ManualReview. |
| Insufficient USD liquidity | Ledger reserve fails with InsufficientFunds; orchestrator sets ManualReview. |
| Payout fails after FX executed | Retries with backoff; after max retries, ManualReview (no automatic FX reversal in POC). |
| Duplicate request (same idempotency key) | Rails/FX/Ledger return prior result; no double posting or double payout. |
| Partial FX fill | Status set to ManualReview; partial amounts recorded in ledger. |

## Manual review triggers

- FX quote expired after N re-quotes  
- FX execution failed  
- Reserve failed (insufficient liquidity)  
- Payout failed after max retries  
- Partial FX fill  

## Recovery

- **ManualReview**: Operator inspects transfer and either retries (e.g. trigger payout again with same idempotency) or reverses/corrects via ledger and external rails.
- **Failed**: Transfer is terminal; investigate logs and correlation ID; fix data or compensate manually.
