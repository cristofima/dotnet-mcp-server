using McpServer.BackendApi.Configuration;
using McpServer.Shared.Configuration;
using McpServer.Shared.Extensions;
using McpServer.Shared.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Hosting;

namespace McpServer.BackendApi.Extensions;

/// <summary>
/// Extension methods for configuring JWT Bearer authentication with Microsoft Entra ID.
/// All configuration must be provided via appsettings.json - no hardcoded defaults.
/// </summary>
public static class AuthenticationExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds JWT Bearer authentication configured for Microsoft Entra ID.
        /// </summary>
        public void AddConfiguredAuthentication(IConfiguration configuration,
            IHostEnvironment environment)
        {
            services.AddEntraIdAuthentication(configuration, environment);
            services.AddHostedService<JwtBearerWarmupService>();
        }

        /// <summary>
        /// Adds JWT Bearer authentication configured for Entra ID.
        /// </summary>
        private void AddEntraIdAuthentication(IConfiguration configuration,
            IHostEnvironment environment)
        {
            var entraIdOptions = configuration.GetRequiredSection<EntraIdApiOptions>(EntraIdBaseOptions.SectionName);

            var authority = entraIdOptions.GetAuthority();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.MetadataAddress = entraIdOptions.GetOpenIdConnectMetadataAddress();
                    options.Authority = authority;
                    options.Audience = entraIdOptions.Audience;
                    options.RequireHttpsMetadata = !environment.IsDevelopment();

                    var tvp = entraIdOptions.BuildTokenValidationParameters();
                    // Accept both api://<client-id> and plain <client-id> (GUID) audience forms.
                    // Entra ID v2 may issue either depending on whether the identifier URI was set
                    // at the time MSAL cached the OBO token.
                    var clientId = entraIdOptions.Audience.Replace("api://", string.Empty, StringComparison.OrdinalIgnoreCase);
                    tvp.ValidAudiences = [$"api://{clientId}", clientId];
                    options.TokenValidationParameters = tvp;
                    options.Events = JwtBearerEventFactory.Create("EntraId");
                });
        }
    }
}
