namespace McpServer.Application.Abstractions;

/// <summary>
/// Interface for token exchange between the MCP Server and backend APIs.
/// This prevents the "token passthrough" anti-pattern by requesting new tokens
/// with the appropriate audience for downstream APIs.
/// </summary>
/// <remarks>
/// Implementations:
/// - EntraIdTokenExchangeService: Uses OAuth 2.0 On-Behalf-Of (OBO) flow
///
/// All methods require an explicit <see cref="CancellationToken"/> parameter (no default value).
/// In an MCP Server, the SDK always injects the token from the client request, so every caller
/// must propagate it. Callers that do not need cancellation should pass
/// <see cref="CancellationToken.None"/> explicitly (TAP guidelines, CA2016, CA1068).
/// </remarks>
public interface ITokenExchangeService
{
    /// <summary>
    /// Exchanges the user's access token for a new token scoped to the downstream API.
    /// </summary>
    /// <param name="subjectToken">The user's access token from the incoming request.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A new access token for the target API, or null if exchange fails.</returns>
    /// <remarks>
    /// The target audience is determined by the implementation via configured scopes
    /// (e.g., <c>DownstreamApiOptions.Scopes</c>). In MSAL OBO, the audience is encoded
    /// within the scopes (<c>api://{client-id}/.default</c>), so a separate audience
    /// parameter is not needed.
    /// </remarks>
    Task<string?> ExchangeTokenAsync(string subjectToken, CancellationToken cancellationToken);
}
