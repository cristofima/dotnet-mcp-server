using System.ComponentModel.DataAnnotations;

using McpBaseline.Shared.Configuration;

namespace McpBaseline.Infrastructure.Configuration;

/// <summary>
/// Microsoft Entra ID configuration for MCP Server (confidential client).
/// Used by the MCP Server to authenticate as a confidential client and perform token exchange (OBO flow).
/// </summary>
public class EntraIdServerOptions : EntraIdBaseOptions
{
    /// <summary>
    /// Application (client) ID for confidential client authentication.
    /// Required for MCP Server.
    /// </summary>
    [Required(ErrorMessage = "EntraId:ClientId is required for MCP Server (confidential client)")]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Client secret for confidential client authentication.
    /// Required for MCP Server.
    /// </summary>
    /// <remarks>
    /// <b>Production:</b> Use Azure Key Vault or managed identity, never commit secrets to source control.
    /// </remarks>
    [Required(ErrorMessage = "EntraId:ClientSecret is required for MCP Server (confidential client)")]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Scopes exposed by this service that clients should request.
    /// Required for RFC 9728 protected resource metadata (<c>scopes_supported</c>).
    /// </summary>
    /// <example>["api://{client-id}/mcp.access"]</example>
    [Required(ErrorMessage = "EntraId:Scopes is required for RFC 9728 protected resource metadata")]
    [MinLength(1, ErrorMessage = "EntraId:Scopes must contain at least one scope")]
    public List<string> Scopes { get; set; } = [];

    /// <summary>
    /// URL to the resource documentation (RFC 9728).
    /// Optional - exposed in protected resource metadata for client discovery.
    /// </summary>
    /// <example>"https://github.com/org/mcp-oauth2-security-baseline"</example>
    [Url(ErrorMessage = "EntraId:ResourceDocumentation must be a valid URL")]
    public string? ResourceDocumentation { get; set; }

    /// <summary>
    /// Gets the Application ID URI (api://{client-id}).
    /// Used for scope URIs and audience configuration.
    /// </summary>
    public string GetApplicationIdUri() => $"api://{ClientId}";
}
