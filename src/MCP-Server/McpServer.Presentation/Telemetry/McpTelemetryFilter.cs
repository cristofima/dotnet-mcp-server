using System.Diagnostics;
using McpServer.Infrastructure.Telemetry;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpServer.Presentation.Telemetry;

/// <summary>
/// MCP CallTool filter that provides automatic telemetry instrumentation
/// (OpenTelemetry activities, metrics, structured logging) for all tool invocations.
/// Registered via WithRequestFilters() in McpServerExtensions.
/// </summary>
public static class McpTelemetryFilter
{
    /// <summary>
    /// Data classification labels per tool. See McpServer.Presentation/README.md § Data Classification.
    /// </summary>
    private static readonly Dictionary<string, string> ToolDataClassifications = new(StringComparer.OrdinalIgnoreCase)
    {
        ["get_backend_users"] = "sensitive",
    };

    public static McpRequestFilter<CallToolRequestParams, CallToolResult> Create()
    {
        return next => async (context, cancellationToken) =>
        {
            var toolName = context.Params?.Name ?? "unknown";
            var stopwatch = Stopwatch.StartNew();
            using var activity = McpActivitySource.StartToolActivity(toolName);

            EnrichActivity(activity, toolName, context.Services);

            var logger = context.Services?.GetService<ILoggerFactory>()?.CreateLogger(typeof(McpTelemetryFilter).FullName!);
            logger?.LogInformation("Tool {ToolName} invoked", toolName);

            try
            {
                var result = await next(context, cancellationToken);
                RecordSuccess(toolName, stopwatch, result);
                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                RecordFailure(toolName, stopwatch, activity, ex);
                throw;
            }
        };
    }

    private static void EnrichActivity(Activity? activity, string toolName, IServiceProvider? services)
    {
        var httpContext = services?.GetService<IHttpContextAccessor>()?.HttpContext;
        McpActivitySource.EnrichWithUserContext(activity, httpContext);

        if (activity is not null)
        {
            SetSessionId(activity, httpContext);
            SetDataClassification(activity, toolName);
        }
    }

    private static void SetSessionId(Activity activity, HttpContext? httpContext)
    {
        if (httpContext?.Request.Headers.TryGetValue("Mcp-Session-Id", out var sessionId) == true)
        {
            var sid = sessionId.ToString();
            if (!string.IsNullOrEmpty(sid))
            {
                activity.SetTag("mcp.session.id", sid);
            }
        }
    }

    private static void SetDataClassification(Activity activity, string toolName)
    {
        if (ToolDataClassifications.TryGetValue(toolName, out var classification))
        {
            activity.SetTag("mcp.tool.data_classification", classification);
        }
    }

    private static void RecordSuccess(string toolName, Stopwatch stopwatch, CallToolResult result)
    {
        stopwatch.Stop();
        McpMetrics.RecordToolInvocation(toolName, true, stopwatch.Elapsed.TotalMilliseconds);

        var responseSize = EstimateResponseSize(result);
        McpMetrics.RecordResponseSize(toolName, responseSize);
    }

    private static void RecordFailure(string toolName, Stopwatch stopwatch, Activity? activity, Exception ex)
    {
        stopwatch.Stop();
        McpActivitySource.RecordError(activity, ex);
        McpMetrics.RecordToolInvocation(toolName, false, stopwatch.Elapsed.TotalMilliseconds);
    }

    private static long EstimateResponseSize(CallToolResult result)
    {
        if (result.Content is null)
        {
            return 0;
        }

        long size = 0;
        foreach (var block in result.Content)
        {
            if (block is TextContentBlock text)
            {
                size += text.Text?.Length ?? 0;
            }
        }
        return size;
    }
}
