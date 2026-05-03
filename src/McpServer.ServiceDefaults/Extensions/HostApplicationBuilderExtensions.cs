using Azure.Monitor.OpenTelemetry.AspNetCore;
using McpServer.ServiceDefaults.Configuration;
using McpServer.ServiceDefaults.Telemetry;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace McpServer.ServiceDefaults.Extensions;

// Registers common Aspire services into the DI container: OpenTelemetry (traces, metrics, logs),
// health check services (AddHealthChecks), service discovery, and HTTP client resilience.
// Does NOT map HTTP endpoints — use WebApplicationExtensions.MapDefaultEndpoints() for that.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class HostApplicationBuilderExtensions
{
    extension<TBuilder>(TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        /// <summary>
        /// Adds common Aspire service defaults with optional custom telemetry configuration.
        /// </summary>
        public void AddServiceDefaults(Action<ServiceTelemetryOptions>? configureTelemetry)
        {
            builder.ConfigureOpenTelemetry(configureTelemetry);

            builder.AddDefaultHealthChecks();

            builder.Services.AddServiceDiscovery();

            builder.Services.ConfigureHttpClientDefaults(http =>
            {
                // Turn on resilience by default
                http.AddStandardResilienceHandler();

                // Turn on service discovery by default
                http.AddServiceDiscovery();
            });

            // Security: Restrict service discovery to HTTPS in production only
            // Development allows both HTTP and HTTPS for local service-to-service communication
            if (!builder.Environment.IsDevelopment())
            {
                builder.Services.Configure<ServiceDiscoveryOptions>(options =>
                {
                    options.AllowedSchemes = ["https"];
                });
            }
        }

        /// <summary>
        /// Configures OpenTelemetry with optional custom telemetry sources.
        /// </summary>
        private void ConfigureOpenTelemetry(Action<ServiceTelemetryOptions>? configureTelemetry)
        {
            var telemetryOptions = new ServiceTelemetryOptions();
            configureTelemetry?.Invoke(telemetryOptions);

            // IHttpContextAccessor is required by HealthCheckActivityFilter to read the
            // request path inside the processor OnStart callback. Idempotent if already registered.
            builder.Services.AddHttpContextAccessor();

            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
            });

            builder.Services.AddOpenTelemetry()
                .WithMetrics(metrics => ConfigureMetrics(metrics, telemetryOptions))
                .WithTracing(tracing => ConfigureTracing(tracing, builder.Environment.ApplicationName, telemetryOptions));

            // Register the health-check trace filter processor. Uses IHttpContextAccessor to
            // read the request path during OnStart before the SDK collects data.
            // Must be registered BEFORE exporters so the processor runs first in the chain.
            // See: https://learn.microsoft.com/azure/azure-monitor/app/opentelemetry-filter#filter-telemetry-using-span-processors
            builder.Services.ConfigureOpenTelemetryTracerProvider((sp, tracing) =>
                tracing.AddProcessor(
                    new HealthCheckActivityFilter(
                        sp.GetRequiredService<IHttpContextAccessor>())));

            builder.AddOpenTelemetryExporters(telemetryOptions);
        }

        private void AddOpenTelemetryExporters(ServiceTelemetryOptions telemetryOptions)
        {
            // OTLP exporter — active when running under Aspire or any OTLP-compatible backend (e.g., local Aspire Dashboard).
            var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
            if (useOtlpExporter)
            {
                builder.Services.AddOpenTelemetry().UseOtlpExporter();
            }

            // Azure Monitor / Application Insights — active when connection string is present.
            // Reads APPLICATIONINSIGHTS_CONNECTION_STRING automatically.
            var useAzureMonitor = !string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]);
            if (useAzureMonitor)
            {
                builder.Services.AddOpenTelemetry().UseAzureMonitor();
            }
        }

        private void AddDefaultHealthChecks()
        {
            builder.Services.AddHealthChecks()
                // Add a default liveness check to ensure app is responsive
                .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
        }
    }

    private static void ConfigureMetrics(MeterProviderBuilder metrics, ServiceTelemetryOptions telemetryOptions)
    {
        metrics.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation();

        foreach (var meterName in telemetryOptions.MeterNames)
        {
            metrics.AddMeter(meterName);
        }
    }

    private static void ConfigureTracing(TracerProviderBuilder tracing, string applicationName, ServiceTelemetryOptions telemetryOptions)
    {
        tracing.AddSource(applicationName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation(options =>
            {
                // Suppress infrastructure HTTP spans (OIDC metadata).
                // OBO token exchange calls are intentionally kept visible.
                options.FilterHttpRequestMessage = ShouldTraceHttpRequest;
            })
            .AddEntityFrameworkCoreInstrumentation();

        foreach (var sourceName in telemetryOptions.ActivitySourceNames)
        {
            tracing.AddSource(sourceName);
        }
    }

    /// <summary>
    /// Filters out infrastructure HTTP spans (OIDC metadata).
    /// OBO token exchange calls to /token are intentionally kept visible.
    /// </summary>
    private static bool ShouldTraceHttpRequest(HttpRequestMessage request)
    {
        var host = request.RequestUri?.Host;
        if (string.IsNullOrEmpty(host))
        {
            return true;
        }

        if (host.Equals("login.microsoftonline.com", StringComparison.OrdinalIgnoreCase))
        {
            return !IsOidcMetadataRequest(request.RequestUri?.AbsolutePath);
        }

        return true;
    }

    private static bool IsOidcMetadataRequest(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        return path.Contains("openid-configuration", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/discovery/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/keys", StringComparison.OrdinalIgnoreCase);
    }
}
