using McpServer.Application.Abstractions;
using McpServer.Application.Configuration;
using Microsoft.Extensions.Options;

namespace McpServer.Infrastructure.Http;

/// <summary>
/// Extracts the caller's bearer token from the current HTTP context and exchanges it
/// for a downstream API token via OBO (On-Behalf-Of / RFC 8693).
/// Token passthrough is NOT supported — OBO exchange must be properly configured.
/// </summary>
public sealed class ApiTokenProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITokenExchangeService _tokenExchangeService;
    private readonly DownstreamApiOptions _options;
    private readonly ILogger<ApiTokenProvider> _logger;

    public ApiTokenProvider(
        IHttpContextAccessor httpContextAccessor,
        ITokenExchangeService tokenExchangeService,
        IOptions<DownstreamApiOptions> options,
        ILogger<ApiTokenProvider> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _tokenExchangeService = tokenExchangeService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Extracts the bearer token from the current HTTP request and exchanges it for a
    /// downstream API access token via OBO. 
    /// </summary>
    /// <returns>The exchanged token or <see langword="null"/> if the token is missing or the exchange fails.</returns>
    public async Task<string?> GetTokenAsync(CancellationToken cancellationToken)
    {
        var userToken = GetUserBearerToken();
        if (string.IsNullOrEmpty(userToken))
        {
            return null;
        }

        var exchangedToken = await _tokenExchangeService.ExchangeTokenAsync(userToken, cancellationToken);

        if (!string.IsNullOrEmpty(exchangedToken))
        {
            _logger.LogDebug(
                "Using exchanged token for downstream API (audience: {Audience})",
                _options.Audience);
            return exchangedToken;
        }

        _logger.LogError(
            "Token exchange failed for audience: {Audience}. " +
            "Verify token exchange is enabled for the server client. " +
            "Token passthrough is disabled for security reasons.",
            _options.Audience);
        return null;
    }

    /// <summary>
    /// Extracts the bearer token from the Authorization header of the current request.
    /// </summary>
    private string? GetUserBearerToken()
    {
        var authHeader = _httpContextAccessor.HttpContext?
            .Request.Headers.Authorization.FirstOrDefault();

        if (string.IsNullOrEmpty(authHeader))
        {
            _logger.LogWarning("No Authorization header found in the request");
            return null;
        }

        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader["Bearer ".Length..].Trim();
        }

        return authHeader;
    }
}
