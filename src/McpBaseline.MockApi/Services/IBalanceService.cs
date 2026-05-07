using McpBaseline.MockApi.Data.Entities;

namespace McpBaseline.MockApi.Services;

/// <summary>
/// Service interface for balance operations.
/// </summary>
public interface IBalanceService
{
    /// <summary>
    /// Gets balance information for a specific project.
    /// </summary>
    Task<BalanceEntity?> GetBalanceByProjectNumberAsync(string projectNumber, CancellationToken cancellationToken);
}
