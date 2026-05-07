using McpBaseline.Application.Abstractions;
using McpBaseline.Application.Models;

namespace McpBaseline.Application.UseCases.Projects;

/// <summary>Retrieves all available projects.</summary>
public sealed class GetProjectsUseCase(IDownstreamApiService downstreamApiService)
{
    /// <summary>Executes the use case.</summary>
    public async Task<McpToolResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await downstreamApiService.GetProjectsAsync(cancellationToken);
        return McpToolResult.Ok(result);
    }
}
