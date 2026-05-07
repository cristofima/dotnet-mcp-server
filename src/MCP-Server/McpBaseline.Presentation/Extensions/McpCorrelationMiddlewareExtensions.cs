using McpBaseline.Presentation.Middleware;

namespace McpBaseline.Presentation.Extensions;

/// <summary>
/// Extension methods for registering MCP correlation middleware.
/// </summary>
public static class McpCorrelationMiddlewareExtensions
{
    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// Adds MCP correlation middleware to extract client headers and enrich OpenTelemetry traces.
        /// </summary>
        /// <remarks>
        /// Extracts correlation headers sent by MCP clients:
        /// <list type="bullet">
        /// <item><description>Mcp-Session-Id - Chat conversation identifier (VS Code Copilot)</description></item>
        /// <item><description>traceparent/tracestate - W3C Trace Context (future compatibility)</description></item>
        /// <item><description>x-ms-* Azure SDK headers (Copilot Studio, Azure-hosted clients)</description></item>
        /// <item><description>User-Agent - Client identification</description></item>
        /// </list>
        /// Tags are added to Activity.Current and visible in Aspire Dashboard traces.
        /// </remarks>
        /// <returns>The application builder for method chaining.</returns>
        public void UseMcpCorrelation()
        {
            app.UseMiddleware<McpCorrelationMiddleware>();
        }
    }
}
