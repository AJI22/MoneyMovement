namespace MoneyMovement.Http;

/// <summary>
/// Standard header name for idempotency keys. Callers (orchestrator, Temporal) send the same key on retries so that
/// downstream services (ledger, FX, rails) can return the previous result without duplicate money movement.
/// </summary>
public static class IdempotencyHeader
{
    public const string Name = "Idempotency-Key";

    public static string? Get(HttpRequestMessage request) =>
        request.Headers.TryGetValues(Name, out var values) ? values.FirstOrDefault() : null;
}
