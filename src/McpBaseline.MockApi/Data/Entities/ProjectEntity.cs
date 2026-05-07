namespace McpBaseline.MockApi.Data.Entities;

/// <summary>
/// Project entity for EF Core persistence.
/// Schema constraints are defined in <c>ProjectEntityConfiguration</c> via Fluent API.
/// </summary>
public sealed class ProjectEntity
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Status { get; set; } = "Planning";

    public decimal Budget { get; set; }

    public string Manager { get; set; } = string.Empty;

    public DateTimeOffset StartDate { get; set; }

    /// <summary>Comma-separated list of team members.</summary>
    public string TeamMembers { get; set; } = string.Empty;
}
