using McpServer.Application.Abstractions;
using McpServer.Application.Configuration;
using McpServer.Infrastructure.Identity;
using McpServer.Infrastructure.Configuration;
using McpServer.Shared.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;

namespace McpServer.Infrastructure.Extensions;

/// <summary>
/// Extension methods for configuring Microsoft Entra ID services.
/// </summary>
internal static class IdentityProviderExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds Microsoft Entra ID services with validation on startup.
        /// <param name="configuration">The application configuration.</param>
        /// </summary>
        internal void AddIdentityProvider(IConfiguration configuration)
        {
            // Register configuration options with validation
            services.AddOptions<DownstreamApiOptions>()
                .Bind(configuration.GetSection(DownstreamApiOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // Register Entra ID services
            services.AddEntraIdServices(configuration);
        }

        /// <summary>
        /// Adds Microsoft Entra ID services (MSAL client and token exchange).
        /// <param name="configuration">The application configuration.</param>
        /// </summary>
        private void AddEntraIdServices(IConfiguration configuration)
        {
            // Register EntraId options with validation
            services.AddOptions<EntraIdServerOptions>()
                .Bind(configuration.GetSection(EntraIdBaseOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // Register MSAL Confidential Client Application for OBO flow.
            // Cache isolation: MSAL partitions OBO tokens by SHA-256 of UserAssertion (per-user keying).
            // WithLegacyCacheCompatibility(false) removes ADAL interop overhead — no ADAL side-by-side usage.
            // See McpServer.Presentation/README.md § MSAL OBO Cache Isolation for multi-instance guidance.
            services.AddSingleton<IConfidentialClientApplication>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<EntraIdServerOptions>>().Value;
                return ConfidentialClientApplicationBuilder
                    .Create(options.ClientId)
                    .WithClientSecret(options.ClientSecret)
                    .WithAuthority(new Uri(options.GetAuthority()))
                    .WithLegacyCacheCompatibility(false)
                    .Build();
            });

            // Register Entra ID token exchange service (OAuth 2.0 On-Behalf-Of flow)
            services.AddScoped<ITokenExchangeService, EntraIdTokenExchangeService>();
        }
    }
}
