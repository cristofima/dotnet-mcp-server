using System.ComponentModel.DataAnnotations;

namespace McpServer.Application.Configuration;

/// <summary>
/// Downstream API configuration.
/// </summary>
public sealed class DownstreamApiOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public static string SectionName { get; } = "DownstreamApi";

    /// <summary>
    /// Base URL for the downstream API.
    /// </summary>
    [Required(ErrorMessage = "DownstreamApi:BaseUrl is required")]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Target audience (client_id) for token exchange.
    /// For Entra ID, this should be the API's App ID URI (e.g., "api://{client-id}").
    /// For Keycloak, this should be the client_id of the target API.
    /// </summary>
    [Required(ErrorMessage = "DownstreamApi:Audience is required")]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Scopes to request when exchanging tokens via OBO for the downstream API.
    /// Required per the <see href="https://learn.microsoft.com/entra/identity-platform/v2-oauth2-on-behalf-of-flow">OAuth 2.0 OBO specification</see>.
    /// </summary>
    /// <example>["api://{client-id}/.default"]</example>
    [Required(ErrorMessage = "DownstreamApi:Scopes is required for OBO token exchange")]
    [MinLength(1, ErrorMessage = "DownstreamApi:Scopes must contain at least one scope")]
    public string[] Scopes { get; set; } = [];
}
