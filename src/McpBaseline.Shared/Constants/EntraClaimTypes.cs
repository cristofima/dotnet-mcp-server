namespace McpBaseline.Shared.Constants;

/// <summary>
/// Entra ID claim type constants for claims remapped by the default JWT Bearer handler
/// when <c>MapInboundClaims</c> is <c>true</c> (the .NET default).
/// Use these alongside the short JWT names (<c>"oid"</c>, <c>"tid"</c>, etc.) as fallbacks
/// so claim lookups work regardless of the mapping configuration.
/// </summary>
public static class EntraClaimTypes
{
    /// <summary>
    /// Stable Entra ID object identifier. Remapped from the JWT <c>oid</c> claim.
    /// </summary>
    public static string ObjectId { get; } = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    /// <summary>
    /// Entra ID tenant/directory identifier. Remapped from the JWT <c>tid</c> claim.
    /// </summary>
    public static string TenantId { get; } = "http://schemas.microsoft.com/identity/claims/tenantid";

    /// <summary>
    /// Delegated permission scopes. Remapped from the JWT <c>scp</c> claim.
    /// </summary>
    public static string Scope { get; } = "http://schemas.microsoft.com/identity/claims/scope";
}
