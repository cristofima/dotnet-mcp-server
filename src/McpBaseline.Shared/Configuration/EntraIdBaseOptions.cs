using System.ComponentModel.DataAnnotations;
using Microsoft.IdentityModel.Tokens;

namespace McpBaseline.Shared.Configuration;

/// <summary>
/// Base configuration options for Microsoft Entra ID authentication.
/// Contains common properties required by both server (confidential client) and API (resource server) scenarios.
/// </summary>
public abstract class EntraIdBaseOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public static string SectionName { get; } = "EntraId";

    /// <summary>
    /// Azure AD instance URL (e.g., "https://login.microsoftonline.com/").
    /// Required for all scenarios.
    /// </summary>
    [Required(ErrorMessage = "EntraId:Instance is required")]
    [Url(ErrorMessage = "EntraId:Instance must be a valid URL")]
    public string Instance { get; set; } = "https://login.microsoftonline.com/";

    /// <summary>
    /// Azure AD tenant ID.
    /// Required for all scenarios.
    /// </summary>
    [Required(ErrorMessage = "EntraId:TenantId is required")]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets the authority URL (without /v2.0 suffix).
    /// Used by MSAL for confidential client flow.
    /// </summary>
    public string GetAuthority() => $"{Instance.TrimEnd('/')}/{TenantId}";

    /// <summary>
    /// Gets the v2.0 authority URL.
    /// Used for JWT Bearer authentication in APIs.
    /// </summary>
    public string GetAuthorityV2() => $"{GetAuthority()}/v2.0";

    /// <summary>
    /// Gets valid issuers for token validation.
    /// Returns both v1.0 and v2.0 issuer formats.
    /// </summary>
    public string[] GetValidIssuers()
    {
        return
        [
            $"https://sts.windows.net/{TenantId}/",
            $"https://login.microsoftonline.com/{TenantId}/v2.0"
        ];
    }

    /// <summary>
    /// Gets the OpenID Connect discovery metadata endpoint for v2.0 tokens.
    /// Use this as <see cref="Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions.MetadataAddress"/>.
    /// </summary>
    public string GetOpenIdConnectMetadataAddress()
        => $"{GetAuthorityV2()}/.well-known/openid-configuration";

    /// <summary>
    /// Builds a <see cref="TokenValidationParameters"/> instance with opinionated security defaults for Entra ID.
    /// Caller must set the audience (<see cref="TokenValidationParameters.ValidAudience"/> or
    /// <see cref="TokenValidationParameters.ValidAudiences"/>) after calling this method.
    /// </summary>
    public TokenValidationParameters BuildTokenValidationParameters()
        => new()
        {
            ValidateIssuer           = true,
            ValidIssuers             = GetValidIssuers(),
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true
        };
}
