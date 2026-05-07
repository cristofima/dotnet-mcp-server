using McpBaseline.MockApi.Data;
using McpBaseline.MockApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace McpBaseline.MockApi.Services;

/// <summary>
/// EF Core implementation of balance service.
/// </summary>
public sealed class BalanceService(MockApiDbContext context, ILogger<BalanceService> logger) : IBalanceService
{
    public async Task<BalanceEntity?> GetBalanceByProjectNumberAsync(string projectNumber, CancellationToken cancellationToken)
    {
        logger.LogDebug("Getting balance for project {ProjectNumber}", projectNumber);
        return await context.Balances
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.ProjectNumber == projectNumber, cancellationToken);
    }
}
