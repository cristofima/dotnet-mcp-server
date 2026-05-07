namespace McpBaseline.ServiceDefaults.Configuration;

/// <summary>
/// Options for registering custom OpenTelemetry sources per service.
/// Each service can provide its own ActivitySource and Meter names
/// so distributed tracing and metrics flow across all layers.
/// </summary>
public sealed class ServiceTelemetryOptions
{
    /// <summary>
    /// Custom ActivitySource names to register for distributed tracing.
    /// </summary>
    public List<string> ActivitySourceNames { get; } = [];

    /// <summary>
    /// Custom Meter names to register for metrics collection.
    /// </summary>
    public List<string> MeterNames { get; } = [];
}
