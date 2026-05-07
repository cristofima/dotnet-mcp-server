using McpBaseline.MockApi.Data.Entities;

namespace McpBaseline.MockApi.Services;

/// <summary>
/// Service interface for task management operations.
/// </summary>
public interface ITaskService
{
    /// <summary>
    /// Gets all tasks for a specific user.
    /// </summary>
    Task<IEnumerable<TaskEntity>> GetTasksByUserIdAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a specific task by ID for a user.
    /// </summary>
    Task<TaskEntity?> GetTaskByIdAsync(string userId, string taskId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new task for a user.
    /// </summary>
    Task<TaskEntity> CreateTaskAsync(string userId, string title, string description, string priority, CancellationToken cancellationToken);

    /// <summary>
    /// Updates the status of a task.
    /// </summary>
    Task<TaskEntity?> UpdateTaskStatusAsync(string userId, string taskId, string status, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a task.
    /// </summary>
    Task<bool> DeleteTaskAsync(string userId, string taskId, CancellationToken cancellationToken);
}
