using McpServer.Infrastructure.Configuration;
using McpServer.Presentation.Configuration;
using McpServer.Shared.Configuration;
using McpServer.Shared.Constants;
using McpServer.Shared.Extensions;
using McpServer.Shared.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace McpServer.Presentation.Extensions;

/// <summary>
/// Extension methods for configuring authentication and authorization with Microsoft Entra ID.
/// </summary>
internal static class AuthenticationExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds Microsoft Entra ID JWT Bearer authentication for MCP server.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        /// <param name="environment">The host environment.</param>
        internal void AddMcpAuthentication(
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            var entraIdConfig = configuration.GetRequiredSection<EntraIdServerOptions>(EntraIdBaseOptions.SectionName);

            var authority = entraIdConfig.GetAuthority();

            // Valid audiences: api://{client-id} (Application ID URI) or just the client-id (GUID)
            var validAudiences = new[]
            {
                entraIdConfig.ClientId,
                $"api://{entraIdConfig.ClientId}"
            };

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.MetadataAddress = entraIdConfig.GetOpenIdConnectMetadataAddress();
                    options.Authority = authority;
                    options.Audience = entraIdConfig.ClientId;
                    options.RequireHttpsMetadata = !environment.IsDevelopment();

                    var tvp = entraIdConfig.BuildTokenValidationParameters();
                    tvp.ValidAudiences = validAudiences;
                    options.TokenValidationParameters = tvp;
                    options.Events = JwtBearerEventFactory.Create("EntraId");
                })
                .AddMcp(options =>
                {
                    options.ResourceMetadata = new ProtectedResourceMetadata
                    {
                        ResourceDocumentation = entraIdConfig.ResourceDocumentation,
                        AuthorizationServers = { authority },
                        ScopesSupported = entraIdConfig.Scopes
                    };
                });
        }

        /// <summary>
        /// Adds rate limiting for DDoS protection.
        /// </summary>
        /// <remarks>
        /// Partition key uses the Entra ID Object ID (<c>oid</c> claim) — stable, unique per user and
        /// service principal. Falls back to IP address when the claim is absent (unauthenticated paths).
        /// Values are read from the <c>RateLimit</c> appsettings section so they can be tuned per
        /// environment without a code change.
        /// </remarks>
        /// <param name="configuration">The application configuration.</param>
        internal void AddMcpRateLimiting(IConfiguration configuration)
        {
            services.AddOptions<RateLimitOptions>()
                .Bind(configuration.GetSection(RateLimitOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            var rateLimitConfig = configuration
                .GetSection(RateLimitOptions.SectionName)
                .Get<RateLimitOptions>() ?? new RateLimitOptions();

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                    context => RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.User?.FindFirstValue(EntraClaimTypes.ObjectId)
                                      ?? context.User?.FindFirstValue("oid")
                                      ?? context.Connection.RemoteIpAddress?.ToString()
                                      ?? "anonymous",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = rateLimitConfig.PermitLimit,
                            Window = TimeSpan.FromSeconds(rateLimitConfig.WindowSeconds),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = rateLimitConfig.QueueLimit
                        }));
            });
        }

        /// <summary>
        /// Adds CORS for MCP Inspector and browser clients.
        /// Allowed origins are configured in appsettings.json under "Cors:AllowedOrigins".
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        internal void AddMcpCors(IConfiguration configuration)
        {
            var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

            services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    if (allowedOrigins.Length > 0)
                    {
                        policy.WithOrigins(allowedOrigins)
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowCredentials();
                    }
                    else
                    {
                        // Fallback to development defaults if not configured
                        policy.WithOrigins(
                                "http://localhost:6274",   // MCP Inspector default
                                "http://localhost:5173",   // Vite dev server
                                "http://localhost:3000",   // React dev server
                                "http://127.0.0.1:6274",
                                "http://127.0.0.1:5173",
                                "http://127.0.0.1:3000")
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowCredentials();
                    }
                });
            });
        }
    }
}
