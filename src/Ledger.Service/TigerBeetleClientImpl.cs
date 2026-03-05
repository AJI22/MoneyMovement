namespace Ledger.Service;

/// <summary>
/// Concrete TigerBeetle client for the Ledger Service. Translates our AccountSpec/TransferSpec into TigerBeetle
/// types and maps result codes. TigerBeetle enforces double-entry and balance constraints; duplicate transfer ids
/// return Exists so callers can implement idempotent retries without duplicate money movement.
/// </summary>
public sealed class TigerBeetleClientImpl : ITigerBeetleClient
{
    private readonly global::TigerBeetle.Client _client;
    private const uint LedgerId = 1;
    private const ushort CodeDefault = 1;

    public TigerBeetleClientImpl(string address)
    {
        var clusterId = (UInt128)0;
        var addresses = new[] { string.IsNullOrEmpty(address) ? "3000" : address };
        _client = new global::TigerBeetle.Client(clusterId, addresses);
    }

    public async Task CreateAccountsAsync(IReadOnlyList<AccountSpec> accounts, CancellationToken cancellationToken = default)
    {
        var batch = accounts.Select(a => new global::TigerBeetle.Account
        {
            Id = a.Id,
            Ledger = a.Ledger,
            Code = a.Code,
            Flags = MapFlags(a.Flags),
            Timestamp = 0,
            UserData128 = 0,
            UserData64 = 0,
            UserData32 = 0
        }).ToArray();
        var errors = _client.CreateAccounts(batch);
        if (errors.Length > 0)
        {
            var first = errors[0];
            throw new InvalidOperationException($"CreateAccounts failed: {first.Result} at index {first.Index}");
        }
        await Task.CompletedTask;
    }

    /// <summary>Creates transfers. Maps TigerBeetle result codes: linked_failed (2/3) => ExceedsCredits/ExceedsDebits (insufficient funds).</summary>
    public async Task<CreateTransferResult> CreateTransfersAsync(IReadOnlyList<TransferSpec> transfers, CancellationToken cancellationToken = default)
    {
        var batch = transfers.Select(t => new global::TigerBeetle.Transfer
        {
            Id = t.Id,
            DebitAccountId = t.DebitAccountId,
            CreditAccountId = t.CreditAccountId,
            Amount = t.Amount,
            Ledger = t.Ledger,
            Code = t.Code,
            Flags = MapTransferFlags(t.Flags),
            PendingId = t.PendingId ?? 0,
            Timestamp = 0,
            UserData128 = 0,
            UserData64 = 0,
            UserData32 = 0
        }).ToArray();
        var errors = _client.CreateTransfers(batch);
        if (errors.Length == 0)
        {
            return CreateTransferResult.Ok;
        }
        // Map TigerBeetle result codes: 1=exists (idempotent), 2=exceeds_credits, 3=exceeds_debits (balance constraint)
        var err = errors[0];
        var result = (int)err.Result switch
        {
            1 => CreateTransferResult.Exists,
            2 => CreateTransferResult.ExceedsCredits,
            3 => CreateTransferResult.ExceedsDebits,
            _ => CreateTransferResult.Other
        };
        return result;
    }

    public async Task<AccountBalance?> GetAccountBalanceAsync(ulong accountId, CancellationToken cancellationToken = default)
    {
        var accounts = _client.LookupAccounts(new UInt128[] { accountId });
        if (accounts.Length == 0)
            return null;
        var a = accounts[0];
        return new AccountBalance(
            (UInt128)a.Id,
            a.DebitsPosted,
            a.CreditsPosted,
            a.DebitsPending,
            a.CreditsPending);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static global::TigerBeetle.AccountFlags MapFlags(AccountFlags f)
    {
        var r = global::TigerBeetle.AccountFlags.None;
        if ((f & AccountFlags.DebitsMustNotExceedCredits) != 0) r |= global::TigerBeetle.AccountFlags.DebitsMustNotExceedCredits;
        if ((f & AccountFlags.CreditsMustNotExceedDebits) != 0) r |= global::TigerBeetle.AccountFlags.CreditsMustNotExceedDebits;
        if ((f & AccountFlags.History) != 0) r |= global::TigerBeetle.AccountFlags.History;
        return r;
    }

    /// <summary>Maps our transfer flags (Pending, PostPendingTransfer, VoidPendingTransfer) to TigerBeetle enum.</summary>
    private static global::TigerBeetle.TransferFlags MapTransferFlags(TransferFlags f)
    {
        var r = global::TigerBeetle.TransferFlags.None;
        if ((f & TransferFlags.Pending) != 0) r |= global::TigerBeetle.TransferFlags.Pending;
        if ((f & TransferFlags.PostPendingTransfer) != 0) r |= global::TigerBeetle.TransferFlags.PostPendingTransfer;
        if ((f & TransferFlags.VoidPendingTransfer) != 0) r |= global::TigerBeetle.TransferFlags.VoidPendingTransfer;
        return r;
    }
}
