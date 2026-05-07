using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using McpBaseline.Application.UseCases.Tasks;
using McpBaseline.Domain.Constants;
using McpBaseline.Domain.Rules;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace McpBaseline.Presentation.Tools;

/// <summary>
/// MCP Tools for task management operations.
/// </summary>
[McpServerToolType]
[Authorize]
public sealed class TaskTools(
    GetTasksUseCase getTasksUseCase,
    CreateTaskUseCase createTaskUseCase,
    UpdateTaskStatusUseCase updateTaskStatusUseCase,
    DeleteTaskUseCase deleteTaskUseCase)
{
    /// <summary>
    /// Gets all tasks for the authenticated user from the backend API.
    /// </summary>
    [McpServerTool(
        Name = "get_tasks",
        Title = "Get User Tasks",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Retrieves all tasks for the authenticated user. Returns task ID, title, description, priority (Low/Medium/High), status (Pending/In Progress/Completed), and timestamps.")]
    [Authorize(Roles = Permissions.TASK_READ)]
    public async Task<string> GetTasksAsync(CancellationToken cancellationToken)
    {
        var result = await getTasksUseCase.ExecuteAsync(cancellationToken);
        return result.ToJson();
    }

    /// <summary>
    /// Creates a new task for the authenticated user via the backend API.
    /// </summary>
    [McpServerTool(
        Name = "create_task",
        Title = "Create New Task",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false)]
    [Description("Creates a new task with a title, description, and optional priority. Returns the created task with its generated ID.")]
    [Authorize(Roles = Permissions.TASK_WRITE)]
    public async Task<string> CreateTaskAsync(
        [Description("The title of the task"), Required, MinLength(1), MaxLength(TaskRules.TitleMaxLength)] string title,
        [Description("A detailed description of the task"), Required, MinLength(1)] string description,
        [Description("Priority level: 'Low', 'Medium', or 'High'")] string priority = "Medium",
        CancellationToken cancellationToken = default)
    {
        var result = await createTaskUseCase.ExecuteAsync(title, description, priority, cancellationToken);
        return result.ToJson();
    }

    /// <summary>
    /// Updates the status of an existing task via the backend API.
    /// </summary>
    [McpServerTool(
        Name = "update_task_status",
        Title = "Update Task Status",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Updates the status of an existing task. Valid statuses: 'Pending', 'In Progress', 'Completed'.")]
    [Authorize(Roles = Permissions.TASK_WRITE)]
    public async Task<string> UpdateTaskStatusAsync(
        [Description("The unique ID of the task to update"), Required] string taskId,
        [Description("New status: 'Pending', 'In Progress', or 'Completed'"), Required] string status,
        CancellationToken cancellationToken)
    {
        var result = await updateTaskStatusUseCase.ExecuteAsync(taskId, status, cancellationToken);
        return result.ToJson();
    }

    /// <summary>
    /// Deletes a task via the backend API.
    /// </summary>
    [McpServerTool(
        Name = "delete_task",
        Title = "Delete Task",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Permanently deletes a task. This action cannot be undone.")]
    [Authorize(Roles = Permissions.TASK_WRITE)]
    public async Task<string> DeleteTaskAsync(
        [Description("The unique ID of the task to delete"), Required] string taskId,
        CancellationToken cancellationToken)
    {
        var result = await deleteTaskUseCase.ExecuteAsync(taskId, cancellationToken);
        return result.ToJson();
    }
}
