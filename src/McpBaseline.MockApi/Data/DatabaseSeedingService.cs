namespace McpBaseline.MockApi.Data;

/// <summary>
/// Hosted service that seeds demo data into the InMemory database at application startup.
/// Runs before the application starts accepting requests.
/// </summary>
public sealed class DatabaseSeedingService(
    IServiceProvider serviceProvider,
    ILogger<DatabaseSeedingService> logger) : IHostedService
{
    /// <summary>
    /// Ensures the InMemory database is created and seeds demo data before the application starts.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Seeding demo data...");

        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MockApiDbContext>();

        await context.Database.EnsureCreatedAsync(cancellationToken);

        await Task.Run(() => DbSeeder.SeedData(context), cancellationToken);

        logger.LogInformation("Demo data seeding complete");
    }

    /// <summary>No-op — seeding is a startup-only concern.</summary>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
