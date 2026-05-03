using Microsoft.EntityFrameworkCore;

namespace McpServer.BackendApi.Data;

/// <summary>
/// Hosted service that applies pending migrations and seeds demo data at application startup.
/// Runs before the application starts accepting requests.
/// </summary>
public sealed class DatabaseSeedingService(
    IServiceProvider serviceProvider,
    ILogger<DatabaseSeedingService> logger) : IHostedService
{
    /// <summary>
    /// Applies pending EF Core migrations and seeds demo data before the application starts.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Applying migrations and seeding demo data...");

        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MockApiDbContext>();

        await context.Database.MigrateAsync(cancellationToken);

        await Task.Run(() => DbSeeder.SeedData(context), cancellationToken);

        logger.LogInformation("Database ready");
    }

    /// <summary>No-op — seeding is a startup-only concern.</summary>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
