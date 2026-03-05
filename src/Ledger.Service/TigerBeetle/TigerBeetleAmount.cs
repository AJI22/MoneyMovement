namespace Ledger.Service.TigerBeetle;

/// <summary>
/// Converts between decimal amounts and TigerBeetle's integer units. TigerBeetle stores amounts as unsigned integers;
/// we use 4 decimal places (1 unit = 0.0001), so 100.50 = 1005000 units. This avoids floating-point rounding
/// in the ledger, which is critical for financial correctness.
/// </summary>
public static class TigerBeetleAmount
{
    private const int Scale = 4;
    private const decimal Multiplier = 10000m;

    /// <summary>Converts a non-negative decimal to TigerBeetle units. Negative amounts are invalid for ledger entries.</summary>
    public static ulong FromDecimal(decimal amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be non-negative");
        return (ulong)decimal.Round(amount * Multiplier, 0);
    }

    /// <summary>Converts TigerBeetle units back to decimal for API responses.</summary>
    public static decimal ToDecimal(ulong units)
    {
        return (decimal)units / Multiplier;
    }
}
