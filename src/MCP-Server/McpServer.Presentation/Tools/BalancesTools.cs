using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using McpServer.Application.UseCases.Balances;
using McpServer.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace McpServer.Presentation.Tools;

/// <summary>
/// MCP Tools for balance/financial operations.
/// </summary>
[McpServerToolType]
[Authorize]
public sealed class BalancesTools(
    GetProjectBalanceUseCase getProjectBalanceUseCase)
{
    /// <summary>
    /// Gets the balance information for a project.
    /// </summary>
    [McpServerTool(
        Name = "get_project_balance",
        Title = "Get Project Balance",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Retrieves financial balance for a project including allocated, spent, remaining, committed, and available amounts.")]
    [Authorize(Roles = Permissions.BALANCE_READ)]
    public async Task<string> GetProjectBalanceAsync(
        [Description("The project ID to get balance for"), Required] string projectId,
        CancellationToken cancellationToken)
    {
        var result = await getProjectBalanceUseCase.ExecuteAsync(projectId, cancellationToken);
        return result.ToJson();
    }
}
