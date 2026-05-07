using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace McpBaseline.Infrastructure.Telemetry;

/// <summary>
/// OpenTelemetry Metrics for MCP tool invocations using MCP semantic convention tag names.
/// Downstream HTTP metrics are auto-instrumented by OpenTelemetry.Instrumentation.Http.
/// </summary>
public static class McpMetrics
{
    public static string MeterName { get; } = "McpBaseline.Presentation";
    public static string Version { get; } = "1.0.0";

    public static readonly Meter Instance = new(MeterName, Version);

    /// <summary>
    /// Counter for total tool invocations.
    /// </summary>
    public static readonly Counter<long> ToolInvocations = Instance.CreateCounter<long>(
        "mcp.tool.invocations",
        unit: "{invocations}",
        description: "Total number of MCP tool invocations");

    /// <summary>
    /// Counter for tool errors.
    /// </summary>
    public static readonly Counter<long> ToolErrors = Instance.CreateCounter<long>(
        "mcp.tool.errors",
        unit: "{errors}",
        description: "Total number of MCP tool errors");

    /// <summary>
    /// Histogram for tool execution duration.
    /// </summary>
    public static readonly Histogram<double> ToolDuration = Instance.CreateHistogram<double>(
        "mcp.tool.duration",
        unit: "ms",
        description: "Duration of MCP tool executions in milliseconds");

    /// <summary>
    /// Counter for input validation errors per tool and parameter.
    /// </summary>
    public static readonly Counter<long> ValidationErrors = Instance.CreateCounter<long>(
        "mcp.tool.validation.errors",
        unit: "{errors}",
        description: "Total number of MCP tool input validation errors");

    /// <summary>
    /// Histogram for tool response payload size in bytes.
    /// Tracks large responses that may impact LLM context windows.
    /// </summary>
    public static readonly Histogram<long> ResponseSize = Instance.CreateHistogram<long>(
        "mcp.tool.response.size",
        unit: "By",
        description: "Size of MCP tool response payloads in bytes");

    /// <summary>
    /// Records a tool invocation with MCP semantic convention tags.
    /// </summary>
    public static void RecordToolInvocation(string toolName, bool success, double durationMs)
    {
        var tags = new TagList
        {
            { "mcp.tool.name", toolName },
            { "mcp.tool.success", success }
        };

        ToolInvocations.Add(1, tags);
        ToolDuration.Record(durationMs, tags);

        if (!success)
        {
            ToolErrors.Add(1, tags);
        }
    }

    /// <summary>
    /// Records an input validation error with tool and parameter context.
    /// </summary>
    public static void RecordValidationError(string toolName, string parameterName)
    {
        ValidationErrors.Add(1, new TagList
        {
            { "mcp.tool.name", toolName },
            { "validation.parameter", parameterName }
        });
    }

    /// <summary>
    /// Records the response payload size for a tool invocation.
    /// </summary>
    public static void RecordResponseSize(string toolName, long sizeBytes)
    {
        ResponseSize.Record(sizeBytes, new TagList
        {
            { "mcp.tool.name", toolName }
        });
    }
}
