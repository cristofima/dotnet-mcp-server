using McpServer.Application.Abstractions;
using McpServer.Application.Models;

namespace McpServer.Application.UseCases.Tasks;

/// <summary>Retrieves all tasks for the authenticated user.</summary>
public sealed class GetTasksUseCase(IDownstreamApiService downstreamApiService)
{
    /// <summary>Executes the use case.</summary>
    public async Task<McpToolResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await downstreamApiService.GetTasksAsync(cancellationToken);
        return McpToolResult.Ok(result);
    }
}
