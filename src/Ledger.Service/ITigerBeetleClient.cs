using System;

namespace Ledger.Service;

/// <summary>
/// Abstraction over the TigerBeetle client. TigerBeetle is the double-entry ledger engine: every transfer
/// is a debit on one account and a credit on another. Race conditions are prevented by TigerBeetle's internal
/// ordering and constraints (e.g. ExceedsDebits/ExceedsCredits when balance would go negative).
/// </summary>
public interface ITigerBeetleClient : IAsyncDisposable
{
    /// <summary>Creates ledger accounts. Fails if account id already exists. Used during bootstrap.</summary>
    Task CreateAccountsAsync(IReadOnlyList<AccountSpec> accounts, CancellationToken cancellationToken = default);

    /// <summary>Creates one or more transfers. Returns Exists if transfer id is duplicate (enables idempotent retries).</summary>
    Task<CreateTransferResult> CreateTransfersAsync(IReadOnlyList<TransferSpec> transfers, CancellationToken cancellationToken = default);

    /// <summary>Looks up current balance (posted and pending) for an account by TigerBeetle account id.</summary>
    Task<AccountBalance?> GetAccountBalanceAsync(ulong accountId, CancellationToken cancellationToken = default);
}

/// <summary>Specification for creating a TigerBeetle account. Flags control balance constraints (e.g. no negative).</summary>
public record AccountSpec(
    ulong Id,
    uint Ledger,
    ushort Code,
    AccountFlags Flags);

/// <summary>Account constraints. CreditsMustNotExceedDebits prevents negative balance (e.g. customer funds).</summary>
[Flags]
public enum AccountFlags
{
    None = 0,
    DebitsMustNotExceedCredits = 1,
    CreditsMustNotExceedDebits = 2,
    History = 4
}

/// <summary>Specification for a single double-entry transfer. PendingId used for two-phase (reserve then post/void).</summary>
public record TransferSpec(
    ulong Id,
    ulong DebitAccountId,
    ulong CreditAccountId,
    ulong Amount,
    uint Ledger,
    ushort Code,
    TransferFlags Flags,
    ulong? PendingId = null);

/// <summary>Pending = two-phase transfer; PostPendingTransfer/VoidPendingTransfer complete or cancel it.</summary>
[Flags]
public enum TransferFlags
{
    None = 0,
    Pending = 1,
    PostPendingTransfer = 2,
    VoidPendingTransfer = 4
}

/// <summary>Balance fields from TigerBeetle. Net balance = Credits - Debits for both posted and pending.</summary>
public record AccountBalance(UInt128 AccountId, UInt128 DebitsPosted, UInt128 CreditsPosted, UInt128 DebitsPending, UInt128 CreditsPending);

/// <summary>Result of CreateTransfers. Exists = idempotent duplicate; ExceedsDebits/ExceedsCredits = insufficient funds.</summary>
public enum CreateTransferResult
{
    Ok,
    Exists,
    InsufficientFunds,
    ExceedsCredits,
    ExceedsDebits,
    Other
}
