namespace McpServer.BackendApi.Data.Entities;

/// <summary>
/// User entity for EF Core persistence.
/// Schema constraints are defined in <c>UserEntityConfiguration</c> via Fluent API.
/// </summary>
public sealed class UserEntity
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public DateTimeOffset LastLogin { get; set; }
}
