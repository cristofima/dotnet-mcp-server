using System.Diagnostics;

namespace McpServer.Presentation.Middleware;

/// <summary>
/// Extracts MCP correlation headers from real clients and adds them as OpenTelemetry Activity tags.
/// </summary>
public sealed class McpCorrelationMiddleware
{
    private readonly RequestDelegate _next;

    public McpCorrelationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var activity = Activity.Current;

        if (activity != null)
        {
            // Represents the MCP turn session ID — changes per turn, not per conversation.
            if (context.Request.Headers.TryGetValue("Mcp-Session-Id", out var mcpSessionId))
            {
                activity.SetTag("mcp.session.id", mcpSessionId.ToString());
            }

            // W3C Trace Context headers
            if (context.Request.Headers.TryGetValue("traceparent", out var traceParent))
            {
                activity.SetTag("w3c.traceparent", traceParent.ToString());
            }

            if (context.Request.Headers.TryGetValue("tracestate", out var traceState))
            {
                activity.SetTag("w3c.tracestate", traceState.ToString());
            }

            // W3C Baggage (cross-cutting concerns)
            if (context.Request.Headers.TryGetValue("baggage", out var baggage))
            {
                activity.SetTag("w3c.baggage", baggage.ToString());
            }

            // Azure SDK headers (may appear with Copilot Studio or Azure AI)
            if (context.Request.Headers.TryGetValue("x-ms-client-request-id", out var azureRequestId))
            {
                activity.SetTag("azure.client_request_id", azureRequestId.ToString());
            }

            if (context.Request.Headers.TryGetValue("x-ms-correlation-id", out var azureCorrelationId))
            {
                activity.SetTag("azure.correlation_id", azureCorrelationId.ToString());
            }

            // Extract User-Agent for client identification (standard HTTP header)
            if (context.Request.Headers.TryGetValue("User-Agent", out var userAgent))
            {
                activity.SetTag("http.user_agent", userAgent.ToString());
            }
        }

        await _next(context);
    }
}
