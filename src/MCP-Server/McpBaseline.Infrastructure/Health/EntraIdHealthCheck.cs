using McpBaseline.Infrastructure.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace McpBaseline.Infrastructure.Health;

/// <summary>
/// Health check that verifies connectivity to Microsoft Entra ID.
/// Validates that the OpenID Connect discovery endpoint is accessible.
/// </summary>
/// <remarks>
/// Checks <c>{authority}/v2.0/.well-known/openid-configuration</c> endpoint availability.
/// </remarks>
public sealed class EntraIdHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly EntraIdServerOptions _entraIdOptions;
    private readonly ILogger<EntraIdHealthCheck> _logger;

    public const string HttpClientName = "identity-provider-health";
    private const string ProviderName = "Entra ID";
    private const string ProviderKey = "provider";
    private const string ErrorKey = "error";

    public EntraIdHealthCheck(
        IHttpClientFactory httpClientFactory,
        IOptions<EntraIdServerOptions> entraIdOptions,
        ILogger<EntraIdHealthCheck> logger)
    {
        _httpClientFactory = httpClientFactory;
        _entraIdOptions = entraIdOptions.Value;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var metadataUrl = GetMetadataUrl();

            if (metadataUrl is null)
            {
                return UnhealthyResult($"{ProviderName} configuration is incomplete", "Missing configuration");
            }

            _logger.LogDebug("Checking identity provider health at {Url}", metadataUrl);
            return await PerformCheckAsync(metadataUrl, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return UnhealthyResult($"{ProviderName} health check timed out", "Timeout");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Identity provider health check failed");
            return UnhealthyResult($"{ProviderName} is not accessible: {ex.Message}", ex.Message, ex);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Unexpected error during identity provider health check");
            return UnhealthyResult($"Health check failed: {ex.Message}", ex.Message, ex);
        }
        catch (UriFormatException ex)
        {
            _logger.LogError(ex, "Invalid URI during identity provider health check");
            return UnhealthyResult($"Health check failed: {ex.Message}", ex.Message, ex);
        }
    }

    /// <summary>
    /// Makes the HTTP call to the OIDC metadata endpoint and returns a health result.
    /// </summary>
    private async Task<HealthCheckResult> PerformCheckAsync(Uri metadataUrl, CancellationToken cancellationToken)
    {
        using var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await httpClient.GetAsync(metadataUrl, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return EvaluateOidcMetadata((int)response.StatusCode, content, metadataUrl);
        }

        return HealthCheckResult.Unhealthy(
            $"{ProviderName} returned {response.StatusCode}",
            data: new Dictionary<string, object>
            {
                [ProviderKey] = ProviderName,
                ["statusCode"] = (int)response.StatusCode,
                ["endpoint"] = metadataUrl
            });
    }

    /// <summary>
    /// Validates OIDC metadata content and returns a healthy or degraded result.
    /// </summary>
    private static HealthCheckResult EvaluateOidcMetadata(int statusCode, string content, Uri metadataUrl)
    {
        if (content.Contains("issuer", StringComparison.Ordinal) && content.Contains("token_endpoint", StringComparison.Ordinal))
        {
            return HealthCheckResult.Healthy(
                $"{ProviderName} is accessible",
                data: new Dictionary<string, object>
                {
                    [ProviderKey] = ProviderName,
                    ["endpoint"] = metadataUrl
                });
        }

        return HealthCheckResult.Degraded(
            $"{ProviderName} responded but metadata may be invalid",
            data: new Dictionary<string, object>
            {
                [ProviderKey] = ProviderName,
                ["statusCode"] = statusCode
            });
    }

    /// <summary>
    /// Creates an <see cref="HealthCheckResult.Unhealthy"/> result with the standard provider data keys.
    /// </summary>
    private static HealthCheckResult UnhealthyResult(string message, string errorDescription, Exception? exception = null)
    {
        return HealthCheckResult.Unhealthy(
            message,
            exception: exception,
            data: new Dictionary<string, object>
            {
                [ProviderKey] = ProviderName,
                [ErrorKey] = errorDescription
            });
    }

    private Uri? GetMetadataUrl()
    {
        var authority = _entraIdOptions.GetAuthority();
        if (string.IsNullOrEmpty(authority))
        {
            return null;
        }

        return new Uri($"{authority}/v2.0/.well-known/openid-configuration");
    }
}
