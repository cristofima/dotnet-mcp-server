using McpServer.BackendApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace McpServer.BackendApi.Data.EntityConfigurations;

/// <summary>
/// Fluent API configuration for <see cref="TaskEntity"/>.
/// Centralizes all persistence schema concerns: keys, column types, lengths, and indexes.
/// </summary>
internal sealed class TaskEntityConfiguration : IEntityTypeConfiguration<TaskEntity>
{
    /// <summary>Configures the <see cref="TaskEntity"/> schema mapping.</summary>
    public void Configure(EntityTypeBuilder<TaskEntity> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasMaxLength(50);
        builder.Property(t => t.UserId).IsRequired().HasMaxLength(100);
        builder.Property(t => t.Title).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Description).HasMaxLength(1000);
        builder.Property(t => t.Priority).IsRequired().HasMaxLength(20);
        builder.Property(t => t.Status).IsRequired().HasMaxLength(20);

        builder.HasIndex(t => t.UserId);
        builder.HasIndex(t => t.Status);
    }
}
