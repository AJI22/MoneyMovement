namespace Ledger.Service.TigerBeetle;

/// <summary>
/// Converts between Guid/ints and TigerBeetle's 128-bit ids. TigerBeetle uses unique transfer and account ids
/// to enforce idempotency (duplicate id returns Exists) and to prevent race conditions in the ledger.
/// </summary>
public static class TigerBeetleId
{
    /// <summary>Derives a TigerBeetle id from a Guid (e.g. transfer id). Only first 8 bytes are used for ulong compatibility.</summary>
    public static ulong FromGuid(Guid guid)
    {
        var bytes = guid.ToByteArray();
        return BitConverter.ToUInt64(bytes, 0);
    }

    /// <summary>Deterministic id from a seed; used for bootstrap account ids.</summary>
    public static ulong FromSeed(int seed)
    {
        return (ulong)(seed + 1);
    }
}
