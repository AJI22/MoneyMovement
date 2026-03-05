using Ledger.Service.TigerBeetle;
using Microsoft.EntityFrameworkCore;
using MoneyMovement.Contracts.Dtos;

namespace Ledger.Service;

/// <summary>
/// Implements the Ledger Service business logic. This service is the single source of truth for all money movements:
/// double-entry postings and reservations are persisted in TigerBeetle, which enforces balance constraints and
/// prevents race conditions. Idempotency is enforced via IdempotencyRecords so that Temporal or HTTP retries do not
/// create duplicate transfers. Reservations exist to lock funds (e.g. for payout) until release or final posting.
/// </summary>
public class LedgerServiceApp : ILedgerService
{
    private readonly LedgerDbContext _db;
    private readonly ITigerBeetleClient _tb;
    private const uint LedgerId = 1;
    private const ushort CodeDefault = 1;

    public LedgerServiceApp(LedgerDbContext db, ITigerBeetleClient tb)
    {
        _db = db;
        _tb = tb;
    }

    /// <summary>Creates TigerBeetle accounts and DB mappings for known logical accounts. Safe to run multiple times; skips existing.</summary>
    public async Task BootstrapAsync(CancellationToken cancellationToken = default)
    {
        // Logical accounts with balance constraints: customer/funds and reserve accounts cannot go negative (CreditsMustNotExceedDebits)
        var accounts = new[]
        {
            ("NGN_BANK", AccountFlags.None),
            ("USD_BANK", AccountFlags.None),
            ("CUSTOMER_FUNDS_NGN", AccountFlags.CreditsMustNotExceedDebits),
            ("FX_POOL_NGN", AccountFlags.None),
            ("FX_POOL_USD", AccountFlags.DebitsMustNotExceedCredits),
            ("FEES_REVENUE", AccountFlags.None),
            ("FX_PNL", AccountFlags.None),
            ("REVERSALS_LOSSES", AccountFlags.None),
            ("USD_PAYOUT_RESERVE", AccountFlags.CreditsMustNotExceedDebits)
        };
        var existing = await _db.AccountMappings.ToListAsync(cancellationToken);
        var toCreate = accounts
            .Select((a, i) => (Name: a.Item1, Flags: a.Item2, Id: (ulong)(i + 1)))
            .Where(a => !existing.Any(e => e.InternalName == a.Name))
            .ToList();
        if (toCreate.Count == 0)
            return;
        var specs = toCreate.Select(a => new AccountSpec(a.Id, LedgerId, CodeDefault, a.Flags)).ToList();
        await _tb.CreateAccountsAsync(specs, cancellationToken);
        foreach (var a in toCreate)
        {
            _db.AccountMappings.Add(new AccountMapping { InternalName = a.Name, TigerBeetleAccountId = a.Id });
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Posts one debit and one credit (double-entry). Idempotent: same key returns success without re-posting.</summary>
    public async Task<LedgerPostingResponse> PostAsync(LedgerPostingRequest request, string? idempotencyKey, string? correlationId, CancellationToken cancellationToken = default)
    {
        // Idempotency: in distributed systems retries are inevitable; the same request may be sent twice. Returning the same result without re-posting prevents duplicate money movement.
        var key = idempotencyKey ?? Guid.NewGuid().ToString("N");
        var existing = await _db.IdempotencyRecords.FindAsync(new object[] { request.TransferId, request.OperationType, key }, cancellationToken);
        if (existing != null)
            return new LedgerPostingResponse(Guid.Empty, true);

        // Double-entry invariant: total debits must equal total credits so the ledger stays balanced.
        decimal totalDebit = 0, totalCredit = 0;
        foreach (var e in request.Entries)
        {
            totalDebit += e.Debit;
            totalCredit += e.Credit;
        }
        if (totalDebit != totalCredit)
            return new LedgerPostingResponse(Guid.Empty, false, "IMBALANCE");

        var mappings = await _db.AccountMappings.ToDictionaryAsync(x => x.InternalName, x => x.TigerBeetleAccountId, cancellationToken);
        var debits = request.Entries.Where(e => e.Debit > 0).ToList();
        var credits = request.Entries.Where(e => e.Credit > 0).ToList();
        if (debits.Count != 1 || credits.Count != 1)
            return new LedgerPostingResponse(Guid.Empty, false, "REQUIRE_ONE_DEBIT_ONE_CREDIT");
        var debitEntry = debits[0];
        var creditEntry = credits[0];
        if (debitEntry.Debit != creditEntry.Credit)
            return new LedgerPostingResponse(Guid.Empty, false, "IMBALANCE");
        if (!mappings.TryGetValue(debitEntry.Account, out var debitAccountId) || !mappings.TryGetValue(creditEntry.Account, out var creditAccountId))
            return new LedgerPostingResponse(Guid.Empty, false, "UNKNOWN_ACCOUNT");
        var amount = TigerBeetleAmount.FromDecimal(debitEntry.Debit);
        if (amount == 0)
            return new LedgerPostingResponse(Guid.Empty, false, "NO_ENTRIES");
        var transferId = TigerBeetleId.FromGuid(Guid.NewGuid());
        var transfer = new TransferSpec(transferId, debitAccountId, creditAccountId, amount, LedgerId, CodeDefault, TransferFlags.None);
        var result = await _tb.CreateTransfersAsync(new[] { transfer }, cancellationToken);
        // TigerBeetle may return Exists if transfer id was already used (e.g. retry with different idempotency path); treat as success and record idempotency
        if (result == CreateTransferResult.Exists)
        {
            _db.IdempotencyRecords.Add(new IdempotencyRecord { TransferId = request.TransferId, OperationType = request.OperationType, IdempotencyKey = key, ProcessedAt = DateTimeOffset.UtcNow });
            await _db.SaveChangesAsync(cancellationToken);
            return new LedgerPostingResponse(Guid.NewGuid(), true);
        }
        if (result != CreateTransferResult.Ok)
            return new LedgerPostingResponse(Guid.Empty, false, result == CreateTransferResult.ExceedsDebits ? "InsufficientFunds" : result.ToString());
        _db.IdempotencyRecords.Add(new IdempotencyRecord { TransferId = request.TransferId, OperationType = request.OperationType, IdempotencyKey = key, ProcessedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync(cancellationToken);
        return new LedgerPostingResponse(Guid.NewGuid(), true);
    }

    /// <summary>Reserves funds (locks amount from one account to another). Balance checks are enforced by TigerBeetle; idempotent by key.</summary>
    public async Task<LedgerReserveResponse> ReserveAsync(LedgerReserveRequest request, string? idempotencyKey, string? correlationId, CancellationToken cancellationToken = default)
    {
        var key = idempotencyKey ?? Guid.NewGuid().ToString("N");
        var existing = await _db.IdempotencyRecords.FindAsync(new object[] { request.TransferId, "RESERVE", key }, cancellationToken);
        if (existing != null)
        {
            // Retry: return existing reservation so caller does not create a second reserve (duplicate money lock).
            var res = await _db.Reservations.FirstOrDefaultAsync(r => r.TransferId == request.TransferId, cancellationToken);
            return new LedgerReserveResponse(res?.ReservationId ?? Guid.Empty, true);
        }
        var mappings = await _db.AccountMappings.ToDictionaryAsync(x => x.InternalName, x => x.TigerBeetleAccountId, cancellationToken);
        if (!mappings.TryGetValue(request.FromAccount, out var fromId) || !mappings.TryGetValue(request.ToAccount, out var toId))
            return new LedgerReserveResponse(Guid.Empty, false, "UNKNOWN_ACCOUNT");
        var amount = TigerBeetleAmount.FromDecimal(request.Amount);
        var transferId = TigerBeetleId.FromGuid(Guid.NewGuid());
        var transfer = new TransferSpec(transferId, fromId, toId, amount, LedgerId, CodeDefault, TransferFlags.None);
        var result = await _tb.CreateTransfersAsync(new[] { transfer }, cancellationToken);
        // ExceedsDebits means insufficient balance on source account—TigerBeetle enforces this and prevents race conditions.
        if (result != CreateTransferResult.Ok && result != CreateTransferResult.Exists)
            return new LedgerReserveResponse(Guid.Empty, false, result == CreateTransferResult.ExceedsDebits ? "InsufficientFunds" : result.ToString());
        var reservationId = Guid.NewGuid();
        _db.Reservations.Add(new ReservationRecord
        {
            ReservationId = reservationId,
            TransferId = request.TransferId,
            FromAccount = request.FromAccount,
            ToAccount = request.ToAccount,
            Amount = request.Amount,
            Currency = request.Currency,
            CreatedAt = DateTimeOffset.UtcNow
        });
        _db.IdempotencyRecords.Add(new IdempotencyRecord { TransferId = request.TransferId, OperationType = "RESERVE", IdempotencyKey = key, ProcessedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync(cancellationToken);
        return new LedgerReserveResponse(reservationId, true);
    }

    /// <summary>Releases a reservation (removes tracking record). Call when payout is cancelled; no TigerBeetle transfer is reversed here.</summary>
    public async Task<LedgerReleaseResponse> ReleaseAsync(LedgerReleaseRequest request, string? correlationId, CancellationToken cancellationToken = default)
    {
        var res = await _db.Reservations.FindAsync(new object[] { request.ReservationId }, cancellationToken);
        if (res == null)
            return new LedgerReleaseResponse(false, "NOT_FOUND");
        _db.Reservations.Remove(res);
        await _db.SaveChangesAsync(cancellationToken);
        return new LedgerReleaseResponse(true);
    }

    /// <summary>Returns balance (posted + pending) from TigerBeetle. Used by callers to check sufficient funds before reserve—balance is the single source of truth.</summary>
    public async Task<LedgerBalanceResponse?> GetBalanceAsync(string account, CancellationToken cancellationToken = default)
    {
        var mapping = await _db.AccountMappings.FindAsync(new object[] { account }, cancellationToken);
        if (mapping == null) return null;
        var balance = await _tb.GetAccountBalanceAsync(mapping.TigerBeetleAccountId, cancellationToken);
        if (balance == null) return null;
        var currency = account.Contains("NGN") ? "NGN" : "USD";
        // Net = credits minus debits; both posted and pending contribute to available balance.
        var netPosted = balance.CreditsPosted >= balance.DebitsPosted ? (balance.CreditsPosted - balance.DebitsPosted) : (UInt128)0;
        var netPending = balance.CreditsPending >= balance.DebitsPending ? (balance.CreditsPending - balance.DebitsPending) : (UInt128)0;
        var total = netPosted + netPending;
        var amount = total <= ulong.MaxValue ? TigerBeetleAmount.ToDecimal((ulong)total) : 0m;
        return new LedgerBalanceResponse(account, currency, amount);
    }
}
