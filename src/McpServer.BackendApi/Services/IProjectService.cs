using McpServer.BackendApi.Data.Entities;

namespace McpServer.BackendApi.Services;

/// <summary>
/// Service interface for project operations.
/// </summary>
public interface IProjectService
{
    /// <summary>
    /// Gets all projects.
    /// </summary>
    Task<IEnumerable<ProjectEntity>> GetAllProjectsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets a specific project by ID.
    /// </summary>
    Task<ProjectEntity?> GetProjectByIdAsync(string projectId, CancellationToken cancellationToken);
}
