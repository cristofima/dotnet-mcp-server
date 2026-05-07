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

        try
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MockApiDbContext>();

            await context.Database.MigrateAsync(cancellationToken);

            await Task.Run(() => DbSeeder.SeedData(context), cancellationToken);

            logger.LogInformation("Database ready");
        }
        catch (Exception ex)
        {
            // Log the failure but do not re-throw — a seeding failure must not prevent the
            // application from starting. The health probe (/alive) must remain reachable so
            // App Service keeps the instance alive and does not enter a crash loop.
            // The most common cause in production is a transient Key Vault / managed-identity
            // resolution delay immediately after a deployment or role-assignment change.
            logger.LogError(ex, "Database migration/seeding failed at startup. The application will continue running, but data may be unavailable until the next restart.");
        }
    }

    /// <summary>No-op — seeding is a startup-only concern.</summary>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
