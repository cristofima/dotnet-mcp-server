using McpServer.BackendApi.Models.Responses;
using McpServer.BackendApi.Services;
using McpServer.BackendApi.Constants;
using McpServer.Shared.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace McpServer.BackendApi.Controllers;

/// <summary>
/// Controller for admin-only operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Permissions.ADMIN_ACCESS)]
public sealed class AdminController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IUserService userService, ILogger<AdminController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Gets list of all users. Admin only.
    /// </summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsersAsync(CancellationToken cancellationToken)
    {
        var user = User.GetUserName();
        _logger.LogInformation("GET /api/admin/users called by ADMIN {User}", user);

        var users = await _userService.GetAllUsersAsync(cancellationToken);
        var userList = users.Select(u => new UserInfo(u.Id, u.Username, u.Role, u.LastLogin)).ToList();

        return Ok(new ApiListResponse<UserInfo, AdminListMetadata>(
            new AdminListMetadata(userList.Count, true),
            userList));
    }
}
