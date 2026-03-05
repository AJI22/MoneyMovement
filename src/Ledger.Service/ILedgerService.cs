using MoneyMovement.Contracts.Dtos;

namespace Ledger.Service;

/// <summary>
/// Core ledger API for the Ledger Service. This is the single source of truth for all money movements.
/// Callers (FX Service, rail services, orchestrator) post debits/credits, reserve funds, and query balances here.
/// All operations that move or lock funds use double-entry bookkeeping in TigerBeetle; idempotency keys prevent
/// duplicate postings when the caller or Temporal retries.
/// </summary>
public interface ILedgerService
{
    /// <summary>
    /// Posts a double-entry transfer (one debit, one credit). Used for final settlement and fee postings.
    /// Idempotent when the same idempotencyKey is supplied for the same TransferId and OperationType.
    /// Side effect: writes to TigerBeetle and IdempotencyRecords. Returns existing result on duplicate key.
    /// </summary>
    /// <param name="request">Transfer id, operation type, and ledger entries (must balance).</param>
    /// <param name="idempotencyKey">Optional key; if omitted a new key is generated. Retries must pass the same key.</param>
    /// <param name="correlationId">For tracing; does not affect behavior.</param>
    Task<LedgerPostingResponse> PostAsync(LedgerPostingRequest request, string? idempotencyKey, string? correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reserves funds from one account to another (e.g. USD_PAYOUT_RESERVE). Balance checks are enforced by TigerBeetle:
    /// accounts with CreditsMustNotExceedDebits cannot go negative. Idempotent for the same idempotencyKey.
    /// </summary>
    /// <param name="request">Transfer id, from/to accounts, amount, currency.</param>
    /// <param name="idempotencyKey">Required for safe retries; duplicate key returns existing reservation.</param>
    /// <param name="correlationId">For tracing.</param>
    Task<LedgerReserveResponse> ReserveAsync(LedgerReserveRequest request, string? idempotencyKey, string? correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a reservation (e.g. when a payout is cancelled). Removes the reservation record only; no TigerBeetle transfer.
    /// Not idempotent by design: calling twice with same ReservationId returns NOT_FOUND on second call.
    /// </summary>
    Task<LedgerReleaseResponse> ReleaseAsync(LedgerReleaseRequest request, string? correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns current balance for a logical account (posted + pending). Used by callers to validate sufficient funds before reserving.
    /// </summary>
    /// <param name="account">Internal account name (e.g. USD_BANK, CUSTOMER_FUNDS_NGN).</param>
    Task<LedgerBalanceResponse?> GetBalanceAsync(string account, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates TigerBeetle accounts and DB mappings if they do not exist. Safe to call repeatedly.
    /// </summary>
    Task BootstrapAsync(CancellationToken cancellationToken = default);
}
