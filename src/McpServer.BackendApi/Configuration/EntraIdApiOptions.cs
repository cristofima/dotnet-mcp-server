using System.ComponentModel.DataAnnotations;

using McpServer.Shared.Configuration;

namespace McpServer.BackendApi.Configuration;

/// <summary>
/// Microsoft Entra ID configuration for MockApi (resource server).
/// Used by the MockApi to validate JWT tokens received from the MCP Server.
/// </summary>
public class EntraIdApiOptions : EntraIdBaseOptions
{
    /// <summary>
    /// Expected audience in the JWT token.
    /// Required for API token validation.
    /// </summary>
    /// <remarks>
    /// Common formats:
    /// <list type="bullet">
    /// <item><description>"api://{client-id}" - Application ID URI format</description></item>
    /// <item><description>"{client-id}" - Client ID directly</description></item>
    /// </list>
    /// Must match the audience claim in tokens sent by MCP Server after OBO exchange.
    /// </remarks>
    [Required(ErrorMessage = "EntraId:Audience is required for resource server (API) token validation")]
    public string Audience { get; set; } = string.Empty;
}
