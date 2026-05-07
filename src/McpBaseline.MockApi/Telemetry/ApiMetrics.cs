using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace McpBaseline.MockApi.Telemetry;

/// <summary>
/// OpenTelemetry Metrics for MockAPI backend operations.
/// Provides Layer 3 observability: API endpoint invocations, response status distribution,
/// and endpoint duration histograms visible in Aspire Dashboard.
/// </summary>
public static class ApiMetrics
{
    public static string MeterName => "McpBaseline.MockApi";
    private static string Version => "1.0.0";

    private static readonly Meter Instance = new(MeterName, Version);

    /// <summary>
    /// Counter for total API endpoint invocations.
    /// </summary>
    private static readonly Counter<long> ApiInvocations = Instance.CreateCounter<long>(
        "api.endpoint.invocations",
        unit: "{invocations}",
        description: "Total number of API endpoint invocations");

    /// <summary>
    /// Histogram for API endpoint response duration.
    /// </summary>
    private static readonly Histogram<double> ApiDuration = Instance.CreateHistogram<double>(
        "api.endpoint.duration",
        unit: "ms",
        description: "Duration of API endpoint executions in milliseconds");

    /// <summary>
    /// Counter for API errors by endpoint and status code.
    /// </summary>
    private static readonly Counter<long> ApiErrors = Instance.CreateCounter<long>(
        "api.endpoint.errors",
        unit: "{errors}",
        description: "Total number of API endpoint errors (4xx/5xx)");

    private const int HttpClientErrorThreshold = 400;

    /// <summary>
    /// Records an API endpoint invocation with controller, action, and status code tags.
    /// </summary>
    public static void RecordApiInvocation(string controller, string action, int statusCode, double durationMs)
    {
        var tags = new TagList
        {
            { "api.controller", controller },
            { "api.action", action },
            { "http.response.status_code", statusCode }
        };

        ApiInvocations.Add(1, tags);
        ApiDuration.Record(durationMs, tags);

        if (statusCode >= HttpClientErrorThreshold)
        {
            ApiErrors.Add(1, tags);
        }
    }
}
