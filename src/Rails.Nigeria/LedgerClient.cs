using System.Net.Http.Json;
using MoneyMovement.Contracts.Dtos;

namespace Rails.Nigeria;

/// <summary>Client for posting to Ledger Service. Nigeria rail posts NGN collect entries; idempotency key is passed so retries do not duplicate postings.</summary>
public interface ILedgerClient
{
    Task<LedgerPostingResponse?> PostAsync(LedgerPostingRequest request, string? idempotencyKey, string? correlationId, CancellationToken cancellationToken = default);
}

/// <summary>HTTP client for Ledger Service posting. Forwards idempotency and correlation headers.</summary>
public class LedgerClient : ILedgerClient
{
    private readonly HttpClient _http;

    public LedgerClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<LedgerPostingResponse?> PostAsync(LedgerPostingRequest request, string? idempotencyKey, string? correlationId, CancellationToken cancellationToken = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "ledger/posting");
        if (!string.IsNullOrEmpty(idempotencyKey)) req.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        if (!string.IsNullOrEmpty(correlationId)) req.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);
        req.Content = JsonContent.Create(request);
        var res = await _http.SendAsync(req, cancellationToken);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<LedgerPostingResponse>(cancellationToken);
    }
}
