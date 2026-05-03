namespace McpServer.BackendApi.Models.Responses;

/// <summary>
/// Represents balance information for a project.
/// </summary>
public record BalanceDetails(
    decimal Allocated,
    decimal Spent,
    decimal Remaining,
    decimal Committed,
    decimal Available,
    string Currency,
    DateTimeOffset LastUpdated
);

/// <summary>
/// Metadata for balance responses containing project context.
/// </summary>
public record BalanceMetadata(string ProjectNumber);
