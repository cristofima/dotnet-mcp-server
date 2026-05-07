using McpServer.BackendApi.Models.Responses;
using McpServer.BackendApi.Services;
using McpServer.Shared.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace McpServer.BackendApi.Controllers;

/// <summary>
/// Controller for project-related operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(IProjectService projectService, ILogger<ProjectsController> logger)
    {
        _projectService = projectService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all projects.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetProjectsAsync(CancellationToken cancellationToken)
    {
        var user = User.GetUserName();

        _logger.LogInformation("GET /api/projects called by {User}", user);

        var projects = await _projectService.GetAllProjectsAsync(cancellationToken);
        var summaries = projects.Select(p => new ProjectSummary(p.Id, p.Name, p.Status, p.Budget)).ToList();

        return Ok(new ApiListResponse<ProjectSummary, ListMetadata>(
            new ListMetadata(summaries.Count), 
            summaries));
    }

    /// <summary>
    /// Gets a project by ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetProjectByIdAsync(string id, CancellationToken cancellationToken)
    {
        var user = User.GetUserName();
        
        _logger.LogInformation("GET /api/projects/{Id} called by {User}", id, user);

        var project = await _projectService.GetProjectByIdAsync(id, cancellationToken);

        if (project == null)
        {
            return NotFound(new { error = "Project not found", id });
        }

        var response = new ProjectWithDetails(
            project.Id,
            project.Name,
            project.Status,
            project.Budget,
            new ProjectDetails(
                project.Manager,
                project.StartDate,
                project.TeamMembers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            )
        );

        return Ok(new ApiResponse<ProjectWithDetails, EmptyMetadata>(new EmptyMetadata(), response));
    }
}
