namespace McpBaseline.MockApi.Models;

/// <summary>
/// Request to create a new task.
/// </summary>
public record CreateTaskRequest(
    string Title,
    string Description,
    string Priority
);

/// <summary>
/// Request to update task status.
/// </summary>
public record UpdateTaskStatusRequest(
    string Status
);
