using McpServer.Application.Abstractions;
using McpServer.Application.Models;

namespace McpServer.Application.UseCases.Projects;

/// <summary>Retrieves detailed information for a specific project.</summary>
public sealed class GetProjectDetailsUseCase(IDownstreamApiService downstreamApiService)
{
    /// <summary>Validates input and retrieves project details via the downstream API.</summary>
    public async Task<McpToolResult> ExecuteAsync(
        string projectId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            return McpToolResult.ValidationError("Project ID is required", "projectId");
        }

        var result = await downstreamApiService.GetProjectByIdAsync(projectId, cancellationToken);
        return McpToolResult.Ok(result);
    }
}
