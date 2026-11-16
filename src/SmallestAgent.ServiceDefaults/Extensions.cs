using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
namespace SmallestAgent.ServiceDefaults;
public static class Extensions
{
    public static T AddServiceDefaults<T>(this T builder) where T : IHostApplicationBuilder
    {
        builder.Services.AddServiceDiscovery();
        // Model POSTs must not be replayed automatically. Cancellation and
        // deadlines belong to the API/client; no global HTTP retry handler.
        builder.Services.ConfigureHttpClientDefaults(http => http.AddServiceDiscovery());
        builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
        builder.Logging.AddOpenTelemetry(log => { log.IncludeFormattedMessage = true; log.IncludeScopes = true; });
        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddRuntimeInstrumentation())
            .WithTracing(traces => traces.AddSource(builder.Environment.ApplicationName).AddSource("com.anthropic.sdk.csharp*").AddAspNetCoreInstrumentation().AddHttpClientInstrumentation());
        if (!string.IsNullOrEmpty(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"])) builder.Services.AddOpenTelemetry().UseOtlpExporter();
        return builder;
    }
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/alive", new HealthCheckOptions { Predicate = check => check.Tags.Contains("live") });
        return app;
    }
}
