using McpServer.BackendApi.Data;
using McpServer.BackendApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace McpServer.BackendApi.Services;

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

    public async Task<(bool Success, string? Error)> TransferAsync(
        string sourceProjectId,
        string targetProjectId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var source = await context.Balances
            .FirstOrDefaultAsync(b => b.ProjectNumber == sourceProjectId, cancellationToken);

        if (source is null)
            return (false, $"Source project '{sourceProjectId}' not found");

        var target = await context.Balances
            .FirstOrDefaultAsync(b => b.ProjectNumber == targetProjectId, cancellationToken);

        if (target is null)
            return (false, $"Target project '{targetProjectId}' not found");

        if (source.Available < amount)
            return (false, $"Insufficient available balance in '{sourceProjectId}'. Available: {source.Available:C}, Requested: {amount:C}");

        source.Available -= amount;
        source.Remaining -= amount;
        source.LastUpdated = DateTimeOffset.UtcNow;

        target.Available += amount;
        target.Remaining += amount;
        target.Allocated += amount;
        target.LastUpdated = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Budget transfer: {Amount:C} from {Source} to {Target}",
            amount, sourceProjectId, targetProjectId);

        return (true, null);
    }
}
