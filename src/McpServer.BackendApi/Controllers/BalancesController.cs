using McpServer.BackendApi.Models;
using McpServer.BackendApi.Models.Responses;
using McpServer.BackendApi.Services;
using McpServer.Shared.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace McpServer.BackendApi.Controllers;

/// <summary>
/// Controller for balance-related operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
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

    /// <summary>
    /// Transfers budget from one project to another.
    /// </summary>
    [HttpPost("transfer")]
    public async Task<IActionResult> TransferAsync([FromBody] TransferRequest request, CancellationToken cancellationToken)
    {
        var user = User.GetUserName();
        _logger.LogInformation(
            "POST /api/balances/transfer called by {User}: {Amount:C} from {Source} to {Target}",
            user, request.Amount, request.SourceProjectId, request.TargetProjectId);

        if (request.Amount <= 0)
            return BadRequest(new { error = "Amount must be greater than zero" });

        if (string.IsNullOrWhiteSpace(request.SourceProjectId) || string.IsNullOrWhiteSpace(request.TargetProjectId))
            return BadRequest(new { error = "SourceProjectId and TargetProjectId are required" });

        if (string.Equals(request.SourceProjectId, request.TargetProjectId, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Source and target projects must be different" });

        var (success, error) = await _balanceService.TransferAsync(
            request.SourceProjectId, request.TargetProjectId, request.Amount, cancellationToken);

        if (!success)
            return BadRequest(new { error });

        return Ok(new
        {
            message = $"Successfully transferred {request.Amount:C} from {request.SourceProjectId} to {request.TargetProjectId}",
            sourceProjectId = request.SourceProjectId,
            targetProjectId = request.TargetProjectId,
            amount = request.Amount
        });
    }
}
