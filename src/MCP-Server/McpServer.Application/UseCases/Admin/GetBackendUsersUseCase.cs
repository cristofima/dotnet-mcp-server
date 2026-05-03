using McpServer.Application.Abstractions;
using McpServer.Application.Models;

namespace McpServer.Application.UseCases.Admin;

/// <summary>Retrieves all system users (admin only).</summary>
public sealed class GetBackendUsersUseCase(IDownstreamApiService downstreamApiService)
{
    /// <summary>Executes the use case.</summary>
    public async Task<McpToolResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await downstreamApiService.GetUsersAsync(cancellationToken);
        return McpToolResult.Ok(result);
    }
}
