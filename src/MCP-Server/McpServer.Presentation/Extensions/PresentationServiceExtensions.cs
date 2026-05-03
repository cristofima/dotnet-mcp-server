namespace McpServer.Presentation.Extensions;

/// <summary>
/// Extension methods for registering Presentation (Server) layer services.
/// </summary>
public static class PresentationServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers all Presentation layer services: authentication, authorization, CORS,
        /// rate limiting, HTTP context accessor, and MCP server with tools and prompts.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        /// <param name="environment">The host environment.</param>
        /// <returns>The service collection for chaining.</returns>
        public void AddPresentation(
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            // Configure authentication with Microsoft Entra ID
            services.AddMcpAuthentication(configuration, environment);

            services.AddAuthorization();

            // Add HttpContextAccessor for token access in services
            services.AddHttpContextAccessor();

            // Add Rate Limiting for DDoS protection
            services.AddMcpRateLimiting(configuration);

            // Add CORS for MCP Inspector and browser clients
            services.AddMcpCors(configuration);

            // Configure MCP Server with tools, prompts, and telemetry filters
            services.AddMcpServerDefaults();
        }
    }
}
