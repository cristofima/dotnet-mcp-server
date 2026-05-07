using McpBaseline.Application.UseCases.Admin;
using McpBaseline.Application.UseCases.Balances;
using McpBaseline.Application.UseCases.Projects;
using McpBaseline.Application.UseCases.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace McpBaseline.Application;

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
