using McpServer.BackendApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace McpServer.BackendApi.Data;

/// <summary>
/// Entity Framework Core DbContext for MockApi.
/// Uses SQL Server. Entity schema constraints are defined
/// in <c>EntityConfigurations/</c> via <see cref="IEntityTypeConfiguration{TEntity}"/>.
/// </summary>
public sealed class MockApiDbContext : DbContext
{
    public MockApiDbContext(DbContextOptions<MockApiDbContext> options) : base(options)
    {
    }

    public DbSet<TaskEntity> Tasks => Set<TaskEntity>();
    public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<BalanceEntity> Balances => Set<BalanceEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MockApiDbContext).Assembly);
    }
}
