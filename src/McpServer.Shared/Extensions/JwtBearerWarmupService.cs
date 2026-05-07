using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace McpServer.Shared.Extensions;

/// <summary>
/// Hosted service that pre-warms the JWT Bearer OIDC metadata after the app starts.
/// Registers work via <see cref="IHostApplicationLifetime.ApplicationStarted"/> so that
/// <see cref="StartAsync"/> returns immediately and does not block Kestrel from accepting
/// connections — blocking startup would cause MCP initialization to time out on the client.
/// </summary>
public sealed class JwtBearerWarmupService : IHostedService
{
    private readonly IOptionsMonitor<JwtBearerOptions> _jwtOptions;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<JwtBearerWarmupService> _logger;

    public JwtBearerWarmupService(
        IOptionsMonitor<JwtBearerOptions> jwtOptions,
        IHostApplicationLifetime lifetime,
        ILogger<JwtBearerWarmupService> logger)
    {
        _jwtOptions = jwtOptions;
        _lifetime = lifetime;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Register warmup to run AFTER the host is fully started and Kestrel is accepting
        // connections. This prevents blocking the startup pipeline.
        _lifetime.ApplicationStarted.Register(() => _ = WarmupAsync());
        return Task.CompletedTask;
    }

    private async Task WarmupAsync()
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
            await configManager.GetConfigurationAsync(CancellationToken.None);
            _logger.LogInformation("JWT Bearer OIDC metadata cached successfully.");
        }
        catch (Exception ex)
        {
            // Log and continue — a failed warmup should not prevent the app from serving requests.
            // The JWT Bearer middleware will retry lazily on the first real request.
            _logger.LogWarning(ex, "JWT Bearer OIDC metadata warmup failed. Metadata will be fetched lazily on first request.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
