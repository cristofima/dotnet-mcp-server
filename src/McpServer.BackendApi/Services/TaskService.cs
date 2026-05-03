using McpServer.BackendApi.Data;
using McpServer.BackendApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace McpServer.BackendApi.Services;

/// <summary>
/// EF Core implementation of task service.
/// Uses InMemory database for persistence.
/// </summary>
public sealed class TaskService : ITaskService
{
    private readonly MockApiDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TaskService> _logger;

    public TaskService(MockApiDbContext context, TimeProvider timeProvider, ILogger<TaskService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<IEnumerable<TaskEntity>> GetTasksByUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting tasks for user {UserId}", userId);
        return await _context.Tasks
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<TaskEntity?> GetTaskByIdAsync(string userId, string taskId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting task {TaskId} for user {UserId}", taskId, userId);
        return await _context.Tasks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId, cancellationToken);
    }

    public async Task<TaskEntity> CreateTaskAsync(string userId, string title, string description, string priority, CancellationToken cancellationToken)
    {
        var task = new TaskEntity
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Title = title,
            Description = description,
            Priority = priority,
            Status = "Pending",
            CreatedAt = _timeProvider.GetUtcNow()
        };

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Created task {TaskId} for user {UserId}", task.Id, userId);
        return task;
    }

    public async Task<TaskEntity?> UpdateTaskStatusAsync(string userId, string taskId, string status, CancellationToken cancellationToken)
    {
        var task = await _context.Tasks
            .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId, cancellationToken);

        if (task == null)
        {
            _logger.LogWarning("Task {TaskId} not found for user {UserId}", taskId, userId);
            return null;
        }

        task.Status = status;
        task.CompletedAt = status == "Completed" ? _timeProvider.GetUtcNow() : null;
        
        await _context.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Updated task {TaskId} status to {Status}", taskId, status);
        return task;
    }

    public async Task<bool> DeleteTaskAsync(string userId, string taskId, CancellationToken cancellationToken)
    {
        var task = await _context.Tasks
            .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId, cancellationToken);

        if (task == null)
        {
            _logger.LogWarning("Task {TaskId} not found for deletion for user {UserId}", taskId, userId);
            return false;
        }

        _context.Tasks.Remove(task);
        await _context.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Deleted task {TaskId} for user {UserId}", taskId, userId);
        return true;
    }
}
