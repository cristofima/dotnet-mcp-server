namespace McpBaseline.MockApi.Models.Responses;

/// <summary>
/// Represents a project summary in list responses.
/// </summary>
public record ProjectSummary(
    string Id,
    string Name,
    string Status,
    decimal Budget
);

/// <summary>
/// Represents project detail information.
/// </summary>
public record ProjectDetails(
    string Manager,
    DateTimeOffset StartDate,
    string[] Team
);

/// <summary>
/// Represents a full project with details.
/// </summary>
public record ProjectWithDetails(
    string Id,
    string Name,
    string Status,
    decimal Budget,
    ProjectDetails Details
);
