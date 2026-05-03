using System.Text.Json;

namespace McpServer.Application.Abstractions;

/// <summary>
/// Contract for downstream API communication.
/// Demonstrates the flow: MCP Client → MCP Server → Backend API.
/// </summary>
/// <remarks>
/// All methods require an explicit <see cref="CancellationToken"/> parameter (no default value).
/// In an MCP Server, the SDK always injects the token from the client request, so every caller
/// must propagate it. Callers that do not need cancellation should pass
/// <see cref="CancellationToken.None"/> explicitly (TAP guidelines, CA2016, CA1068).
/// </remarks>
public interface IDownstreamApiService
{
    /// <summary>
    /// Gets the list of projects from the Mock API.
    /// </summary>
    Task<JsonElement> GetProjectsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets project details by ID.
    /// </summary>
    Task<JsonElement> GetProjectByIdAsync(string projectId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets balance information for a project.
    /// </summary>
    Task<JsonElement> GetBalanceAsync(string projectId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets all tasks for the authenticated user.
    /// </summary>
    Task<JsonElement> GetTasksAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets a specific task by ID.
    /// </summary>
    Task<JsonElement> GetTaskByIdAsync(string taskId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new task.
    /// </summary>
    Task<JsonElement> CreateTaskAsync(string title, string description, string priority, CancellationToken cancellationToken);

    /// <summary>
    /// Updates the status of a task.
    /// </summary>
    Task<JsonElement> UpdateTaskStatusAsync(string taskId, string status, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a task.
    /// </summary>
    Task<JsonElement> DeleteTaskAsync(string taskId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets all users (admin only).
    /// </summary>
    Task<JsonElement> GetUsersAsync(CancellationToken cancellationToken);
}
