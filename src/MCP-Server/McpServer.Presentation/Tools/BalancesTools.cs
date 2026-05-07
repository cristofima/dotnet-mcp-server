using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using McpServer.Application.UseCases.Balances;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace McpServer.Presentation.Tools;

/// <summary>
/// MCP Tools for balance/financial operations.
/// </summary>
[McpServerToolType]
[Authorize]
public sealed class BalancesTools(
    GetProjectBalanceUseCase getProjectBalanceUseCase,
    TransferBudgetUseCase transferBudgetUseCase)
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
    public async Task<string> GetProjectBalanceAsync(
        [Description("The project ID to get balance for"), Required] string projectId,
        CancellationToken cancellationToken)
    {
        var result = await getProjectBalanceUseCase.ExecuteAsync(projectId, cancellationToken);
        return result.ToJson();
    }

    /// <summary>
    /// Transfers budget from one project to another.
    /// </summary>
    [McpServerTool(
        Name = "transfer_budget",
        Title = "Transfer Budget",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false,
        OpenWorld = false)]
    [Description("Transfers budget from one project to another. This action modifies financial data and cannot be undone. Only call this tool after the user has explicitly confirmed the transfer.")]
    public async Task<string> TransferBudgetAsync(
        [Description("The project ID to transfer budget from"), Required] string sourceProjectId,
        [Description("The project ID to transfer budget to"), Required] string targetProjectId,
        [Description("The amount to transfer (must be greater than zero)"), Required] decimal amount,
        CancellationToken cancellationToken)
    {
        var result = await transferBudgetUseCase.ExecuteAsync(sourceProjectId, targetProjectId, amount, cancellationToken);
        return result.ToJson();
    }
}
