using McpBaseline.Application.Abstractions;
using McpBaseline.Application.Models;
using McpBaseline.Domain.Rules;

namespace McpBaseline.Application.UseCases.Tasks;

/// <summary>Updates the status of an existing task after validating domain rules.</summary>
public sealed class UpdateTaskStatusUseCase(IDownstreamApiService downstreamApiService)
{
    /// <summary>Validates input and updates task status via the downstream API.</summary>
    public async Task<McpToolResult> ExecuteAsync(
        string taskId,
        string status,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return McpToolResult.ValidationError("Task ID is required", "taskId");
        }

        if (!TaskRules.IsValidStatus(status))
        {
            return McpToolResult.ValidationError(
                $"Status must be one of: {TaskRules.ValidStatusesList}", "status");
        }

        var result = await downstreamApiService.UpdateTaskStatusAsync(taskId, status, cancellationToken);
        return McpToolResult.Ok(result);
    }
}
