using McpServer.Application.UseCases.Admin;
using McpServer.Application.UseCases.Balances;
using McpServer.Application.UseCases.Projects;
using McpServer.Application.UseCases.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace McpServer.Application;

/// <summary>
/// Extension methods for registering Application layer services.
/// </summary>
public static class ApplicationServiceExtensions
{
    /// <summary>
    /// Registers Application layer services: use cases for each domain.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Task use cases
        services.AddTransient<GetTasksUseCase>();
        services.AddTransient<CreateTaskUseCase>();
        services.AddTransient<UpdateTaskStatusUseCase>();
        services.AddTransient<DeleteTaskUseCase>();

        // Project use cases
        services.AddTransient<GetProjectsUseCase>();
        services.AddTransient<GetProjectDetailsUseCase>();

        // Balance use cases
        services.AddTransient<GetProjectBalanceUseCase>();

        // Admin use cases
        services.AddTransient<GetBackendUsersUseCase>();

        return services;
    }
}
