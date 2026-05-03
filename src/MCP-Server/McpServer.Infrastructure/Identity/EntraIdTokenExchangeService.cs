using McpServer.Application.Abstractions;
using McpServer.Application.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;

namespace McpServer.Infrastructure.Identity;

/// <summary>
/// Token exchange service implementation using Microsoft Entra ID On-Behalf-Of (OBO) flow.
/// </summary>
/// <remarks>
/// Implements the OAuth 2.0 OBO flow for Microsoft Entra ID:
/// 1. MCP Server receives a request with user's JWT
/// 2. Uses MSAL to exchange using AcquireTokenOnBehalfOf
/// 3. The new token is used to call the downstream API
/// 
/// MSAL provides automatic token caching - tokens are reused until expiration.
/// </remarks>
public sealed class EntraIdTokenExchangeService : ITokenExchangeService
{
    private readonly IConfidentialClientApplication _confidentialClient;
    private readonly DownstreamApiOptions _downstreamApiOptions;
    private readonly ILogger<EntraIdTokenExchangeService> _logger;

    public EntraIdTokenExchangeService(
        IConfidentialClientApplication confidentialClient,
        IOptions<DownstreamApiOptions> downstreamApiOptions,
        ILogger<EntraIdTokenExchangeService> logger)
    {
        _confidentialClient = confidentialClient;
        _downstreamApiOptions = downstreamApiOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string?> ExchangeTokenAsync(
        string subjectToken,
        CancellationToken cancellationToken)
    {
        try
        {
            // Remove "Bearer " prefix if present
            var rawToken = subjectToken;
            if (rawToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                rawToken = rawToken["Bearer ".Length..].Trim();
            }

            if (string.IsNullOrEmpty(rawToken))
            {
                _logger.LogWarning("EntraId OBO: Empty incoming token");
                return null;
            }

            // Build scopes for the downstream API
            var scopes = GetDownstreamScopes();

            _logger.LogInformation(
                "EntraId OBO: Exchanging token for scopes: {Scopes}",
                string.Join(", ", scopes));

            // Create UserAssertion from the incoming token
            var userAssertion = new UserAssertion(rawToken);

            // MSAL handles token caching automatically
            // If a valid cached token exists, it will be returned instead of making a new request
            var result = await _confidentialClient
                .AcquireTokenOnBehalfOf(scopes, userAssertion)
                .ExecuteAsync(cancellationToken);

            _logger.LogInformation(
                "EntraId OBO successful. Token expires at: {ExpiresOn}",
                result.ExpiresOn);

            return result.AccessToken;
        }
        catch (MsalUiRequiredException ex)
        {
            // User interaction required (consent, MFA, etc.)
            _logger.LogWarning(ex,
                "EntraId OBO requires user interaction: {Error}. Claims: {Claims}",
                ex.Message,
                ex.Claims);
            return null;
        }
        catch (MsalServiceException ex)
        {
            _logger.LogError(ex,
                "EntraId OBO service error: {ErrorCode} - {Message}",
                ex.ErrorCode,
                ex.Message);
            return null;
        }
        catch (MsalException ex)
        {
            _logger.LogError(ex, "EntraId OBO MSAL error: {ErrorCode} - {Message}", ex.ErrorCode, ex.Message);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "EntraId OBO network error: {Message}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Gets the configured scopes for the downstream API.
    /// </summary>
    private string[] GetDownstreamScopes() => _downstreamApiOptions.Scopes;
}
