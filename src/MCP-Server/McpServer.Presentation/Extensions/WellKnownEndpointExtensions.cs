using McpServer.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Serilog;

namespace McpServer.Presentation.Extensions;

/// <summary>
/// Extension methods for mapping RFC 9728 and RFC 8414 discovery endpoints.
/// </summary>
public static class WellKnownEndpointExtensions
{
    private static readonly string[] Handler = ["header"];

    extension(WebApplication app)
    {
        /// <summary>
        /// Maps the OAuth2 well-known discovery endpoints as anonymous.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>RFC 9728: <c>/.well-known/oauth-protected-resource</c> — protected resource metadata</description></item>
        /// <item><description>RFC 8414: <c>/.well-known/oauth-authorization-server</c> — authorization server metadata proxy</description></item>
        /// </list>
        /// </remarks>
        /// <returns>The web application for chaining.</returns>
        public void MapWellKnownEndpoints()
        {
            app.MapProtectedResourceMetadata();
            app.MapAuthorizationServerMetadata();
        }

        /// <summary>
        /// RFC 9728: Protected Resource Metadata endpoint.
        /// </summary>
        private void MapProtectedResourceMetadata()
        {
            app.MapGet("/.well-known/oauth-protected-resource", (
                IOptions<EntraIdServerOptions> entraIdOptions,
                HttpContext ctx) =>
            {
                var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
                var entraIdConfig = entraIdOptions.Value;
                var authServer = entraIdConfig.GetAuthority();

                var response = new Dictionary<string, object>
                {
                    ["resource"] = baseUrl,
                    ["authorization_servers"] = new[] { authServer },
                    ["bearer_methods_supported"] = Handler,
                };

                if (entraIdConfig.Scopes.Count > 0)
                {
                    response["scopes_supported"] = entraIdConfig.Scopes;
                }

                if (!string.IsNullOrEmpty(entraIdConfig.ResourceDocumentation))
                {
                    response["resource_documentation"] = entraIdConfig.ResourceDocumentation;
                }

                return Results.Json(response);
            }).AllowAnonymous();
        }

        /// <summary>
        /// RFC 8414: OAuth Authorization Server Metadata proxy (Entra ID OpenID Configuration).
        /// </summary>
        private void MapAuthorizationServerMetadata()
        {
            app.MapGet("/.well-known/oauth-authorization-server", async (
                IOptions<EntraIdServerOptions> entraIdOptions,
                IHttpClientFactory httpClientFactory,
                CancellationToken cancellationToken) =>
            {
                using var client = httpClientFactory.CreateClient();

                try
                {
                    var url = new Uri($"{entraIdOptions.Value.GetAuthority()}/v2.0/.well-known/openid-configuration");
                    var response = await client.GetStringAsync(url, cancellationToken);
                    return Results.Content(response, "application/json");
                }
                catch (HttpRequestException ex)
                {
                    Log.Warning(ex, "Failed to fetch OAuth AS metadata: {Error}", ex.Message);
                    return Results.Problem("Failed to fetch OAuth Authorization Server metadata");
                }
                catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    Log.Warning(ex, "OAuth AS metadata request timed out: {Error}", ex.Message);
                    return Results.Problem("Failed to fetch OAuth Authorization Server metadata");
                }
            }).AllowAnonymous();
        }
    }
}
