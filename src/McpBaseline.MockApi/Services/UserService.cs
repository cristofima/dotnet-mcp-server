using McpBaseline.MockApi.Data;
using McpBaseline.MockApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace McpBaseline.MockApi.Services;

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
