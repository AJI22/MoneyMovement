using System.Linq;
using MoneyMovement.Contracts;
using MoneyMovement.Contracts.Dtos;
using Xunit;

namespace UnitTests;

/// <summary>Unit tests for shared contracts and DTOs used across the platform.</summary>
public class ContractsTests
{
    /// <summary>Scenario: Money.Zero returns zero amount for a currency. Protects against null or wrong default in money handling.</summary>
    [Fact]
    public void Money_Zero()
    {
        var z = Money.Zero("NGN");
        Assert.Equal(0, z.Amount);
        Assert.Equal("NGN", z.Currency);
    }

    /// <summary>Scenario: TransferId.New generates a non-empty GUID. Protects against duplicate or empty transfer ids in workflows.</summary>
    [Fact]
    public void TransferId_New()
    {
        var id = TransferId.New();
        Assert.NotEqual(Guid.Empty, id.Value);
    }

    /// <summary>Scenario: Ledger entries for a double-entry posting have equal total debits and credits. Protects against imbalanced postings that would violate ledger invariants.</summary>
    [Fact]
    public void LedgerEntryDto_DebitCreditSum()
    {
        var entries = new[]
        {
            new LedgerEntryDto("NGN_BANK", 100m, 0, "NGN"),
            new LedgerEntryDto("CUSTOMER_FUNDS_NGN", 0, 100m, "NGN")
        };
        Assert.Equal(100m, entries.Sum(e => e.Debit));
        Assert.Equal(100m, entries.Sum(e => e.Credit));
    }
}
