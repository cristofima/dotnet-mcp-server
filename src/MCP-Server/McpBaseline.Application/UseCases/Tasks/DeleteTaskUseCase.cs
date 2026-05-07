using McpBaseline.Application.Abstractions;
using McpBaseline.Application.Models;

namespace McpBaseline.Application.UseCases.Tasks;

/// <summary>Deletes a task by ID.</summary>
public sealed class DeleteTaskUseCase(IDownstreamApiService downstreamApiService)
{
    /// <summary>Validates input and deletes the task via the downstream API.</summary>
    public async Task<McpToolResult> ExecuteAsync(
        string taskId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return McpToolResult.ValidationError("Task ID is required", "taskId");
        }

        var result = await downstreamApiService.DeleteTaskAsync(taskId, cancellationToken);
        return McpToolResult.Ok(result);
    }
}
