using McpBaseline.Presentation.Prompts;
using McpBaseline.Presentation.Telemetry;
using McpBaseline.Presentation.Tools;
using ModelContextProtocol.Protocol;

namespace McpBaseline.Presentation.Extensions;

/// <summary>
/// Extension methods for configuring the MCP Server with tools and prompts.
/// </summary>
internal static class McpServerExtensions
{
    private const string ServerName = "mcp-baseline-server";
    private const string ServerTitle = "MCP OAuth2 Security Baseline";
    private const string ServerVersion = "1.0.0";
    private const string ServerDescription =
        "OAuth2-secured MCP server with On-Behalf-Of (OBO) token exchange. " +
        "Exposes tools and prompts for task management, project tracking, balance inquiries, " +
        "identity inspection, and administrative operations — all backed by a downstream API via Microsoft Entra ID.";

    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds and configures the MCP Server with default settings, including server metadata,
        /// HTTP transport, authorization filters, tools, and prompts.
        /// </summary>
        /// <returns>The service collection for chaining.</returns>
        internal void AddMcpServerDefaults()
        {
            services
                .AddMcpServer(options =>
                {
                    options.ServerInfo = new Implementation
                    {
                        Name = ServerName,
                        Title = ServerTitle,
                        Version = ServerVersion,
                        Description = ServerDescription,
                    };
                })
                .WithHttpTransport()
                .AddAuthorizationFilters()
                .WithRequestFilters(filters =>
                {
                    filters.AddCallToolFilter(McpTelemetryFilter.Create());
                })
                // Tools
                .WithTools<TaskTools>()
                .WithTools<ProjectsTools>()
                .WithTools<BalancesTools>()
                .WithTools<AdminTools>()
                // Prompts
                .WithPrompts<TaskPrompts>()
                .WithPrompts<ProjectPrompts>()
                .WithPrompts<AdminPrompts>();
        }
    }
}
