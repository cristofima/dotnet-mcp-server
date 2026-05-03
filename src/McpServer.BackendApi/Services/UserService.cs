using McpServer.BackendApi.Data;
using McpServer.BackendApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace McpServer.BackendApi.Services;

/// <summary>
/// EF Core implementation of user service.
/// </summary>
public sealed class UserService(MockApiDbContext context, ILogger<UserService> logger) : IUserService
{
    public async Task<IEnumerable<UserEntity>> GetAllUsersAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("Getting all users");
        return await context.Users.OrderBy(u => u.Id).AsNoTracking().ToListAsync(cancellationToken);
    }
}
