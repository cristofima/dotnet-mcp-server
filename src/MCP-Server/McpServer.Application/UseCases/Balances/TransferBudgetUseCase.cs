using McpServer.Application.Abstractions;
using McpServer.Application.Models;

namespace McpServer.Application.UseCases.Balances;

/// <summary>Transfers budget from one project to another via the downstream API.</summary>
public sealed class TransferBudgetUseCase(IDownstreamApiService downstreamApiService)
{
    /// <summary>Validates input and performs the budget transfer.</summary>
    public async Task<McpToolResult> ExecuteAsync(
        string sourceProjectId,
        string targetProjectId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceProjectId))
            return McpToolResult.ValidationError("Source project ID is required", "sourceProjectId");

        if (string.IsNullOrWhiteSpace(targetProjectId))
            return McpToolResult.ValidationError("Target project ID is required", "targetProjectId");

        if (amount <= 0)
            return McpToolResult.ValidationError("Amount must be greater than zero", "amount");

        if (string.Equals(sourceProjectId, targetProjectId, StringComparison.OrdinalIgnoreCase))
            return McpToolResult.ValidationError("Source and target projects must be different", "targetProjectId");

        var result = await downstreamApiService.TransferBudgetAsync(sourceProjectId, targetProjectId, amount, cancellationToken);
        return McpToolResult.Ok(result);
    }
}
