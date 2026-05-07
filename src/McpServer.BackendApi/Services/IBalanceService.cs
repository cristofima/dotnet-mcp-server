using McpServer.BackendApi.Data.Entities;

namespace McpServer.BackendApi.Services;

/// <summary>
/// Service interface for balance operations.
/// </summary>
public interface IBalanceService
{
    /// <summary>
    /// Gets balance information for a specific project.
    /// </summary>
    Task<BalanceEntity?> GetBalanceByProjectNumberAsync(string projectNumber, CancellationToken cancellationToken);

    /// <summary>
    /// Transfers the specified amount from the source project's available budget to the target project.
    /// Returns false if either project is not found or the source has insufficient available funds.
    /// </summary>
    Task<(bool Success, string? Error)> TransferAsync(string sourceProjectId, string targetProjectId, decimal amount, CancellationToken cancellationToken);
}
