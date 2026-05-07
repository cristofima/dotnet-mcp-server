using McpBaseline.Application.Abstractions;
using McpBaseline.Application.Models;
using McpBaseline.Domain.Rules;

namespace McpBaseline.Application.UseCases.Tasks;

/// <summary>Creates a new task after validating domain rules.</summary>
public sealed class CreateTaskUseCase(IDownstreamApiService downstreamApiService)
{
    /// <summary>Validates input and creates a task via the downstream API.</summary>
    public async Task<McpToolResult> ExecuteAsync(
        string title,
        string description,
        string priority,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return McpToolResult.ValidationError("Title is required", "title");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return McpToolResult.ValidationError("Description is required", "description");
        }

        if (!TaskRules.IsValidPriority(priority))
        {
            return McpToolResult.ValidationError(
                $"Priority must be one of: {TaskRules.ValidPrioritiesList}", "priority");
        }

        var result = await downstreamApiService.CreateTaskAsync(title, description, priority, cancellationToken);
        return McpToolResult.Ok(result);
    }
}
