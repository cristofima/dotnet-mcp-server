using McpBaseline.MockApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace McpBaseline.MockApi.Data;

/// <summary>
/// Entity Framework Core DbContext for MockApi.
/// Uses PostgreSQL via Npgsql. Entity schema constraints are defined
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
