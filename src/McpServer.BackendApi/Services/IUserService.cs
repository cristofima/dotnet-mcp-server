using McpServer.BackendApi.Data.Entities;

namespace McpServer.BackendApi.Services;

/// <summary>
/// Service interface for user management operations.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Gets all users.
    /// </summary>
    Task<IEnumerable<UserEntity>> GetAllUsersAsync(CancellationToken cancellationToken);
}
