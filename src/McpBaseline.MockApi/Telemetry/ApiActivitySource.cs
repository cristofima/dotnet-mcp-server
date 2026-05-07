using System.Diagnostics;

namespace McpBaseline.MockApi.Telemetry;

/// <summary>
/// OpenTelemetry ActivitySource for MockAPI backend operations.
/// Provides Layer 3 observability: API endpoint tracing,
/// and downstream correlation via W3C Trace Context (traceparent/tracestate).
/// </summary>
public static class ApiActivitySource
{
    public static string Name => "McpBaseline.MockApi";
    private static string Version => "1.0.0";

    private static readonly ActivitySource Instance = new(Name, Version);

    /// <summary>
    /// Starts a new activity for an API endpoint operation with semantic convention attributes.
    /// Respects parent context from W3C Trace Context for distributed tracing correlation
    /// with the upstream MCP Server (Layer 2).
    /// </summary>
    public static Activity? StartApiActivity(string operationName) =>
        StartApiActivity(operationName, ActivityKind.Server);

    /// <summary>
    /// Starts a new activity for an API endpoint operation with the specified <see cref="ActivityKind"/>.
    /// </summary>
    public static Activity? StartApiActivity(string operationName, ActivityKind kind)
    {
        var activity = Instance.StartActivity($"api.{operationName}", kind);

        if (activity is null)
        {
            return activity;
        }

        activity.SetTag("api.operation.name", operationName);
        activity.SetTag("service.layer", "backend-api");

        return activity;
    }

    /// <summary>
    /// Records an error on the current activity with OpenTelemetry semantic conventions.
    /// </summary>
    public static void RecordError(Activity? activity, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (activity is null)
        {
            return;
        }

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.SetTag("error.type", exception.GetType().FullName);
        activity.SetTag("exception.type", exception.GetType().Name);
        activity.SetTag("exception.message", exception.Message);
    }
}
