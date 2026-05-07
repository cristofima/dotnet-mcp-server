using System.ComponentModel;
using McpBaseline.Application.UseCases.Admin;
using McpBaseline.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace McpBaseline.Presentation.Tools;

/// <summary>
/// MCP Tools for admin-only operations.
/// </summary>
[McpServerToolType]
[Authorize]
public sealed class AdminTools(
    GetBackendUsersUseCase getBackendUsersUseCase)
{
    /// <summary>
    /// Gets the list of users from the admin endpoint.
    /// </summary>
    /// <remarks>
    /// Data classification: sensitive (user enumeration, PII).
    /// See McpBaseline.Presentation/README.md § Data Classification.
    /// </remarks>
    [McpServerTool(
        Name = "get_backend_users",
        Title = "List System Users",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Retrieves all system users with their ID, username, role, and last login. " +
        "Data classification: sensitive (user enumeration). Requires admin:access role.")]
    [Authorize(Roles = Permissions.ADMIN_ACCESS)]
    public async Task<string> GetBackendUsersAsync(CancellationToken cancellationToken)
    {
        var result = await getBackendUsersUseCase.ExecuteAsync(cancellationToken);
        return result.ToJson();
    }
}
