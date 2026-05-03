using McpServer.BackendApi.Data.Entities;
using McpServer.BackendApi.Models;
using McpServer.BackendApi.Models.Responses;
using McpServer.BackendApi.Services;
using McpServer.BackendApi.Constants;
using McpServer.Shared.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace McpServer.BackendApi.Controllers;

/// <summary>
/// Controller for task management operations.
/// Provides CRUD operations for user tasks.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly ILogger<TasksController> _logger;

    private static readonly string[] ValidPriorities = ["Low", "Medium", "High"];
    private static readonly string[] ValidStates = ["Pending", "In Progress", "Completed"];

    public TasksController(ITaskService taskService, ILogger<TasksController> logger)
    {
        _taskService = taskService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all tasks for the authenticated user.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = Permissions.TASK_READ)]
    public async Task<IActionResult> GetTasksAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserName();
        _logger.LogInformation("GET /api/tasks called by {User}", userId);

        var tasks = await _taskService.GetTasksByUserIdAsync(userId, cancellationToken);
        var taskList = tasks.Select(ToResponse).ToList();

        return Ok(new ApiListResponse<TaskItemResponse, ListMetadata>(
            new ListMetadata(taskList.Count), 
            taskList));
    }

    /// <summary>
    /// Gets a specific task by ID.
    /// </summary>
    [HttpGet("{taskId}")]
    [Authorize(Roles = Permissions.TASK_READ)]
    public async Task<IActionResult> GetTaskByIdAsync(string taskId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserName();
        _logger.LogInformation("GET /api/tasks/{TaskId} called by {User}", taskId, userId);

        var task = await _taskService.GetTaskByIdAsync(userId, taskId, cancellationToken);

        if (task == null)
        {
            return NotFound(new { error = "Task not found", taskId, userId });
        }

        return Ok(new ApiResponse<TaskItemResponse, EmptyMetadata>(new EmptyMetadata(), ToResponse(task)));
    }

    /// <summary>
    /// Creates a new task.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Permissions.TASK_WRITE)]
    public async Task<IActionResult> CreateTaskAsync([FromBody] CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserName();
        _logger.LogInformation("POST /api/tasks called by {User} - Title: {Title}", userId, request.Title);

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new { error = "Title is required" });
        }

        if (!ValidPriorities.Contains(request.Priority, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = $"Invalid priority. Must be one of: {string.Join(", ", ValidPriorities)}" });
        }

        var task = await _taskService.CreateTaskAsync(userId, request.Title, request.Description ?? "", request.Priority, cancellationToken);

        return Created(new Uri($"/api/tasks/{task.Id}", UriKind.Relative), new ApiResponse<TaskItemResponse, EmptyMetadata>(new EmptyMetadata(), ToResponse(task)));
    }

    /// <summary>
    /// Updates the status of a task.
    /// </summary>
    [HttpPatch("{taskId}/status")]
    [Authorize(Roles = Permissions.TASK_WRITE)]
    public async Task<IActionResult> UpdateTaskStatusAsync(string taskId, [FromBody] UpdateTaskStatusRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserName();
        _logger.LogInformation("PATCH /api/tasks/{TaskId}/status called by {User} - Status: {Status}", taskId, userId, request.Status);

        if (!ValidStates.Contains(request.Status, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = $"Invalid status. Must be one of: {string.Join(", ", ValidStates)}" });
        }

        var task = await _taskService.UpdateTaskStatusAsync(userId, taskId, request.Status, cancellationToken);

        if (task == null)
        {
            return NotFound(new { error = "Task not found", taskId, userId });
        }

        return Ok(new ApiResponse<TaskItemResponse, EmptyMetadata>(new EmptyMetadata(), ToResponse(task)));
    }

    /// <summary>
    /// Deletes a task.
    /// </summary>
    [HttpDelete("{taskId}")]
    [Authorize(Roles = Permissions.TASK_WRITE)]
    public async Task<IActionResult> DeleteTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserName();
        _logger.LogInformation("DELETE /api/tasks/{TaskId} called by {User}", taskId, userId);

        var deleted = await _taskService.DeleteTaskAsync(userId, taskId, cancellationToken);

        if (!deleted)
        {
            return NotFound(new { error = "Task not found", taskId, userId });
        }

        return Ok(new ApiResponse<string, TaskDeleteMetadata>(
            new TaskDeleteMetadata(taskId), 
            "Task deleted successfully"));
    }

    private static TaskItemResponse ToResponse(TaskEntity task) =>
        new(task.Id, task.UserId, task.Title, task.Description, task.Priority, task.Status, task.CreatedAt, task.CompletedAt);
}
