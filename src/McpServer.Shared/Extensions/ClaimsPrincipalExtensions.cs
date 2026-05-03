using System.Security.Claims;

namespace McpServer.Shared.Extensions;

/// <summary>
/// Extension methods for ClaimsPrincipal.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    private const string AnonymousUser = "anonymous";

    extension(ClaimsPrincipal user)
    {
        /// <summary>
        /// Gets the current user's name from the claims principal.
        /// </summary>
        public string GetUserName()
        {
            return user.Identity?.Name ?? user.FindFirst("preferred_username")?.Value ?? AnonymousUser;
        }
    }
}
