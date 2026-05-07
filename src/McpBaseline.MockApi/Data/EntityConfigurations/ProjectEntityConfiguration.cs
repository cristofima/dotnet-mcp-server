using McpBaseline.MockApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace McpBaseline.MockApi.Data.EntityConfigurations;

/// <summary>
/// Fluent API configuration for <see cref="ProjectEntity"/>.
/// Centralizes all persistence schema concerns: keys, column types, lengths, and indexes.
/// </summary>
internal sealed class ProjectEntityConfiguration : IEntityTypeConfiguration<ProjectEntity>
{
    /// <summary>Configures the <see cref="ProjectEntity"/> schema mapping.</summary>
    public void Configure(EntityTypeBuilder<ProjectEntity> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasMaxLength(20);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Status).IsRequired().HasMaxLength(50);
        builder.Property(p => p.Manager).HasMaxLength(100);
        builder.Property(p => p.TeamMembers).HasMaxLength(500);

        builder.HasIndex(p => p.Status);
    }
}
