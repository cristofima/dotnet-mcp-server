namespace McpBaseline.MockApi.Models.Responses;

/// <summary>
/// Task item in API responses.
/// </summary>
public record TaskItemResponse(
    string Id,
    string UserId,
    string Title,
    string Description,
    string Priority,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt
);

/// <summary>
/// Metadata for task deletion responses.
/// </summary>
public record TaskDeleteMetadata(string TaskId);
