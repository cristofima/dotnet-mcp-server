namespace McpBaseline.MockApi.Models.Responses;

/// <summary>
/// Represents user information in admin responses.
/// </summary>
public record UserInfo(
    int Id,
    string Username,
    string Role,
    DateTimeOffset LastLogin
);
