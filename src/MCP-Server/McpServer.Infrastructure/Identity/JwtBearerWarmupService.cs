using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace McpServer.Infrastructure.Identity;

/// <summary>
/// Hosted service that pre-warms the JWT Bearer OIDC metadata on startup.
/// Without this, the first authenticated request triggers a lazy download of
/// the OpenID Connect discovery document, which can take several seconds and
/// cause MCP initialization to time out.
/// </summary>
internal sealed class JwtBearerWarmupService : IHostedService
{
    private readonly IOptionsMonitor<JwtBearerOptions> _jwtOptions;
    private readonly ILogger<JwtBearerWarmupService> _logger;

    public JwtBearerWarmupService(
        IOptionsMonitor<JwtBearerOptions> jwtOptions,
        ILogger<JwtBearerWarmupService> logger)
    {
        _jwtOptions = jwtOptions;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = _jwtOptions.Get(JwtBearerDefaults.AuthenticationScheme);

        if (options.ConfigurationManager is not IConfigurationManager<OpenIdConnectConfiguration> configManager)
        {
            _logger.LogWarning("JWT Bearer ConfigurationManager is not available — skipping OIDC metadata warmup.");
            return;
        }

        try
        {
            _logger.LogInformation("Pre-warming JWT Bearer OIDC metadata from {MetadataAddress}", options.MetadataAddress);
            await configManager.GetConfigurationAsync(cancellationToken);
            _logger.LogInformation("JWT Bearer OIDC metadata cached successfully.");
        }
        catch (Exception ex)
        {
            // Log and continue — a failed warmup should not prevent startup.
            // The JWT Bearer middleware will retry lazily on the first real request.
            _logger.LogWarning(ex, "JWT Bearer OIDC metadata warmup failed. Metadata will be fetched lazily on first request.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
