using System.Net.Http.Json;
using MoneyMovement.Contracts.Dtos;

namespace FX.Service;

/// <summary>Client for posting double-entry entries to the Ledger Service. Used by FX Service when executing conversion.</summary>
public interface ILedgerClient
{
    /// <summary>Posts a ledger entry. Pass idempotency key so retries do not create duplicate postings. Returns null on HTTP failure.</summary>
    Task<LedgerPostingResponse?> PostAsync(LedgerPostingRequest request, string? idempotencyKey, string? correlationId, CancellationToken cancellationToken = default);
}

/// <summary>HTTP client that calls Ledger Service posting endpoint. Forwards idempotency and correlation headers.</summary>
public class LedgerClient : ILedgerClient
{
    private readonly HttpClient _http;

    public LedgerClient(HttpClient http) => _http = http;

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
