using McpBaseline.MockApi.Data;
using McpBaseline.MockApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace McpBaseline.MockApi.Services;

/// <summary>
/// EF Core implementation of project service.
/// Uses InMemory database for persistence.
/// </summary>
public sealed class ProjectService : IProjectService
{
    private readonly MockApiDbContext _context;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(MockApiDbContext context, ILogger<ProjectService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<ProjectEntity>> GetAllProjectsAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting all projects");
        return await _context.Projects
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<ProjectEntity?> GetProjectByIdAsync(string projectId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting project {ProjectId}", projectId);
        return await _context.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);
    }
}
