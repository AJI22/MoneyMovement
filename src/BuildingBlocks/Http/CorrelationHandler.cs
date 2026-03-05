using Microsoft.AspNetCore.Http;
using MoneyMovement.Observability;

namespace MoneyMovement.Http;

public class CorrelationHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public CorrelationHandler(IHttpContextAccessor? httpContextAccessor = null)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var correlationId = _httpContextAccessor?.HttpContext?.Request.Headers[CorrelationMiddleware.HeaderName].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");
        request.Headers.TryAddWithoutValidation(CorrelationMiddleware.HeaderName, correlationId);
        return await base.SendAsync(request, cancellationToken);
    }
}
