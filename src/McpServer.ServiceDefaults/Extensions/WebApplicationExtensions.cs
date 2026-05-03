using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace McpServer.ServiceDefaults.Extensions;

// Maps health check HTTP endpoints (/health readiness, /alive liveness) into the request pipeline.
// Requires health check services to be registered first via HostApplicationBuilderExtensions.AddServiceDefaults().
// See https://aka.ms/dotnet/aspire/service-defaults
public static class WebApplicationExtensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    extension(WebApplication app)
    {
        /// <summary>Maps health check endpoints for liveness and readiness.</summary>
        /// <returns>The web application for chaining.</returns>
        public void MapDefaultEndpoints()
        {
            // Health-check traces suppressed by HealthCheckActivityFilter — see README § Health Check Trace Filtering.

            // Liveness: only "self" tag, no external dependencies.
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live"),
                ResponseWriter = WriteMinimalResponse
            }).AllowAnonymous();

            // Readiness: all registered checks. Detailed response in dev, minimal in prod.
            app.MapHealthChecks(HealthEndpointPath, new HealthCheckOptions
            {
                ResponseWriter = app.Environment.IsDevelopment()
                    ? WriteDetailedResponse
                    : WriteMinimalResponse
            }).AllowAnonymous();
        }
    }

    /// <summary>
    /// Minimal health response — returns only the aggregate status string (Healthy/Degraded/Unhealthy).
    /// Safe for production: no internal component names, no exception details, no timing data.
    /// </summary>
    private static Task WriteMinimalResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var status = report.Status.ToString();
        return context.Response.WriteAsync($"{{\"status\":\"{status}\"}}");
    }

    /// <summary>
    /// Detailed health response — includes per-check status, duration, and description.
    /// Only used in development (Aspire Dashboard, local debugging).
    /// </summary>
    private static Task WriteDetailedResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var entries = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            duration = e.Value.Duration.TotalMilliseconds,
            description = e.Value.Description
        });

        var result = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            checks = entries
        };

        return context.Response.WriteAsJsonAsync(result);
    }
}
