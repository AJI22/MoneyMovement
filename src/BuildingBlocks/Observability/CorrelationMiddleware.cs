using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace MoneyMovement.Observability;

/// <summary>Provides correlation id for request tracing across services. If the client sends X-Correlation-ID it is preserved; otherwise a new one is generated and set on response and Activity.</summary>
public static class CorrelationMiddleware
{
    public const string HeaderName = "X-Correlation-ID";

    public static string GetOrCreate(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var existing) &&
            !string.IsNullOrWhiteSpace(existing))
            return existing!;

        var correlationId = Guid.NewGuid().ToString("N");
        context.Response.Headers.Append(HeaderName, correlationId);
        context.Items[HeaderName] = correlationId;
        return correlationId;
    }

    public static void UseCorrelationId(this IApplicationBuilder app)
    {
        app.Use((context, next) =>
        {
            var correlationId = GetOrCreate(context);
            context.Response.Headers[HeaderName] = correlationId;
            Activity.Current?.SetTag("correlation.id", correlationId);
            return next(context);
        });
    }
}
