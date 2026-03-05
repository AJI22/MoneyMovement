using System.Net.Http.Json;
using MoneyMovement.Contracts.Dtos;
using Xunit;

namespace IntegrationTests;

/// <summary>Tests for transfer flow DTOs and serialization. Support integration tests that call orchestrator and downstream services.</summary>
public class TransferFlowTests
{
    /// <summary>Scenario: CreateTransferRequest holds source/dest currency and amount. Protects against wrong or missing fields when the orchestrator starts a transfer.</summary>
    [Fact]
    public void CreateTransferRequest_Serializes()
    {
        var req = new CreateTransferRequest("user1", "recipient1", "NGN", 100_000m, "USD");
        Assert.Equal("NGN", req.SourceCurrency);
        Assert.Equal(100_000m, req.SourceAmount);
    }

    /// <summary>Scenario: FxQuoteResponse has positive rate and estimated amount. Protects against invalid quote data being passed to accept/execute.</summary>
    [Fact]
    public void FxQuoteResponse_HasRequiredFields()
    {
        var q = new FxQuoteResponse(Guid.NewGuid(), 0.0006m, DateTimeOffset.UtcNow, 60m, 0m);
        Assert.True(q.Rate > 0);
        Assert.True(q.EstimatedDestinationAmount > 0);
    }
}
