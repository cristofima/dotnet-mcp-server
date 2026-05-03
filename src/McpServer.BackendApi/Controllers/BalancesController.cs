using McpServer.BackendApi.Models.Responses;
using McpServer.BackendApi.Services;
using McpServer.BackendApi.Constants;
using McpServer.Shared.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace McpServer.BackendApi.Controllers;

/// <summary>
/// Controller for balance-related operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Permissions.BALANCE_READ)]
public sealed class BalancesController : ControllerBase
{
    private readonly IBalanceService _balanceService;
    private readonly ILogger<BalancesController> _logger;

    public BalancesController(IBalanceService balanceService, ILogger<BalancesController> logger)
    {
        _balanceService = balanceService;
        _logger = logger;
    }

    /// <summary>
    /// Gets balance information for a specific project.
    /// </summary>
    [HttpGet("{projectNumber}")]
    public async Task<IActionResult> GetBalancesAsync(string projectNumber, CancellationToken cancellationToken)
    {
        var user = User.GetUserName();
        _logger.LogInformation("GET /api/balances/{ProjectNumber} called by {User}", projectNumber, user);

        var entity = await _balanceService.GetBalanceByProjectNumberAsync(projectNumber, cancellationToken);

        if (entity == null)
        {
            return NotFound(new { error = "Balance not found", projectNumber });
        }

        var balances = new BalanceDetails(
            Allocated: entity.Allocated,
            Spent: entity.Spent,
            Remaining: entity.Remaining,
            Committed: entity.Committed,
            Available: entity.Available,
            Currency: entity.Currency,
            LastUpdated: entity.LastUpdated
        );

        return Ok(new ApiResponse<BalanceDetails, BalanceMetadata>(
            new BalanceMetadata(projectNumber),
            balances));
    }
}
