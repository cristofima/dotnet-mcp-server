using System.Text.Json;
using McpBaseline.Application.Abstractions;

namespace McpBaseline.Infrastructure.Http;

/// <summary>
/// Downstream REST API client.
/// Inherits reusable HTTP/token infrastructure from <see cref="AuthenticatedApiClient"/>,
/// exposing only domain-specific routes and operations.
/// </summary>
public sealed class DownstreamApiService : AuthenticatedApiClient, IDownstreamApiService
{
    private static class Routes
    {
        public static string Projects { get; } = "/api/projects";
        public static string Balances { get; } = "/api/balances";
        public static string Tasks { get; } = "/api/tasks";
        public static string AdminUsers { get; } = "/api/admin/users";
    }

    public DownstreamApiService(
        HttpClient httpClient,
        ApiTokenProvider tokenProvider,
        ILogger<DownstreamApiService> logger)
        : base(httpClient, tokenProvider, logger)
    { }

    public Task<JsonElement> GetProjectsAsync(CancellationToken cancellationToken) =>
        GetAsync(Routes.Projects, cancellationToken);

    public Task<JsonElement> GetProjectByIdAsync(string projectId, CancellationToken cancellationToken) =>
        GetAsync($"{Routes.Projects}/{projectId}", cancellationToken);

    public Task<JsonElement> GetBalanceAsync(string projectId, CancellationToken cancellationToken) =>
        GetAsync($"{Routes.Balances}/{projectId}", cancellationToken);

    public Task<JsonElement> GetUsersAsync(CancellationToken cancellationToken) =>
        GetAsync(Routes.AdminUsers, cancellationToken);

    public Task<JsonElement> GetTasksAsync(CancellationToken cancellationToken) =>
        GetAsync(Routes.Tasks, cancellationToken);

    public Task<JsonElement> GetTaskByIdAsync(string taskId, CancellationToken cancellationToken) =>
        GetAsync($"{Routes.Tasks}/{taskId}", cancellationToken);

    public Task<JsonElement> CreateTaskAsync(string title, string description, string priority, CancellationToken cancellationToken) =>
        PostAsync(Routes.Tasks, new { title, description, priority }, cancellationToken);

    public Task<JsonElement> UpdateTaskStatusAsync(string taskId, string status, CancellationToken cancellationToken) =>
        PatchAsync($"{Routes.Tasks}/{taskId}/status", new { status }, cancellationToken);

    public Task<JsonElement> DeleteTaskAsync(string taskId, CancellationToken cancellationToken) =>
        DeleteAsync($"{Routes.Tasks}/{taskId}", cancellationToken);
}
