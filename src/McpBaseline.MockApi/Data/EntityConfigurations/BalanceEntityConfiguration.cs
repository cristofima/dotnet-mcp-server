using McpBaseline.MockApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace McpBaseline.MockApi.Data.EntityConfigurations;

/// <summary>
/// Fluent API configuration for <see cref="BalanceEntity"/>.
/// Centralizes all persistence schema concerns: keys, column types, lengths, and indexes.
/// </summary>
internal sealed class BalanceEntityConfiguration : IEntityTypeConfiguration<BalanceEntity>
{
    /// <summary>Configures the <see cref="BalanceEntity"/> schema mapping.</summary>
    public void Configure(EntityTypeBuilder<BalanceEntity> builder)
    {
        builder.HasKey(b => b.ProjectNumber);

        builder.Property(b => b.ProjectNumber).HasMaxLength(20);
        builder.Property(b => b.Currency).IsRequired().HasMaxLength(10);

        builder.HasIndex(b => b.ProjectNumber);
    }
}
