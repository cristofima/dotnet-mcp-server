using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using McpBaseline.Application.UseCases.Projects;
using McpBaseline.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace McpBaseline.Presentation.Tools;

/// <summary>
/// MCP Tools for project operations.
/// </summary>
[McpServerToolType]
[Authorize]
public sealed class ProjectsTools(
    GetProjectsUseCase getProjectsUseCase,
    GetProjectDetailsUseCase getProjectDetailsUseCase)
{
    /// <summary>
    /// Gets the list of projects from the backend API.
    /// </summary>
    [McpServerTool(
        Name = "get_projects",
        Title = "List Projects",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Retrieves all available projects with their ID, name, status, and budget.")]
    [Authorize(Roles = Permissions.PROJECT_READ)]
    public async Task<string> GetProjectsAsync(CancellationToken cancellationToken)
    {
        var result = await getProjectsUseCase.ExecuteAsync(cancellationToken);
        return result.ToJson();
    }

    /// <summary>
    /// Gets details of a specific project from the backend API.
    /// </summary>
    [McpServerTool(
        Name = "get_project_details",
        Title = "Get Project Details",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Retrieves detailed project information including manager, start date, and team members.")]
    [Authorize(Roles = Permissions.PROJECT_READ)]
    public async Task<string> GetProjectDetailsAsync(
        [Description("The project ID to retrieve"), Required] string projectId,
        CancellationToken cancellationToken)
    {
        var result = await getProjectDetailsUseCase.ExecuteAsync(projectId, cancellationToken);
        return result.ToJson();
    }
}
