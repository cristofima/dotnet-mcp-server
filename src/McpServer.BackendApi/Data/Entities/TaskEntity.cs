namespace McpServer.BackendApi.Data.Entities;

/// <summary>
/// Task entity for EF Core persistence.
/// Schema constraints are defined in <c>TaskEntityConfiguration</c> via Fluent API.
/// </summary>
public sealed class TaskEntity
{
    public string Id { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Priority { get; set; } = "Medium";

    public string Status { get; set; } = "Pending";
    
    public DateTimeOffset CreatedAt { get; set; }
    
    public DateTimeOffset? CompletedAt { get; set; }
}
