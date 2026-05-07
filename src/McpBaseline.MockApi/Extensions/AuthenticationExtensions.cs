using McpBaseline.MockApi.Configuration;
using McpBaseline.Shared.Configuration;
using McpBaseline.Shared.Extensions;
using McpBaseline.Shared.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace McpBaseline.MockApi.Extensions;

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
                    tvp.ValidAudience = entraIdOptions.Audience;
                    options.TokenValidationParameters = tvp;
                    options.Events = JwtBearerEventFactory.Create("EntraId");
                });
        }
    }
}
