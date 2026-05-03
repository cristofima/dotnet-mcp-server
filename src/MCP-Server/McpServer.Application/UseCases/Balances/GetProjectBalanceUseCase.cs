using McpServer.Application.Abstractions;
using McpServer.Application.Models;

namespace McpServer.Application.UseCases.Balances;

/// <summary>Retrieves financial balance for a project.</summary>
public sealed class GetProjectBalanceUseCase(IDownstreamApiService downstreamApiService)
{
    /// <summary>Validates input and retrieves project balance via the downstream API.</summary>
    public async Task<McpToolResult> ExecuteAsync(
        string projectId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            return McpToolResult.ValidationError("Project ID is required", "projectId");
        }

        var result = await downstreamApiService.GetBalanceAsync(projectId, cancellationToken);
        return McpToolResult.Ok(result);
    }
}
