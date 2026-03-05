using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Extensions.Hosting;

namespace MoneyMovement.Observability;

public static class ObservabilityExtensions
{
    public static IHostApplicationBuilder AddObservability(this IHostApplicationBuilder builder, string serviceName)
    {
        var resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddService(serviceName);

        builder.Services.AddOpenTelemetry()
            .WithTracing(t =>
            {
                t.SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
                var otlp = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
                if (!string.IsNullOrEmpty(otlp))
                    t.AddOtlpExporter();
                else
                    t.AddConsoleExporter();
            })
            .WithMetrics(m =>
            {
                m.SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
                var otlp = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
                if (!string.IsNullOrEmpty(otlp))
                    m.AddOtlpExporter();
                else
                    m.AddPrometheusExporter();
            });

        return builder;
    }

    public static IApplicationBuilder UseObservability(this IApplicationBuilder app)
    {
        app.UseCorrelationId();
        return app;
    }
}
