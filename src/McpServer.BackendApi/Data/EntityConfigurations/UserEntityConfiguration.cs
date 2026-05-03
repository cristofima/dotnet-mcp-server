using McpServer.BackendApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace McpServer.BackendApi.Data.EntityConfigurations;

/// <summary>
/// Fluent API configuration for <see cref="UserEntity"/>.
/// Centralizes all persistence schema concerns: keys, column types, and lengths.
/// </summary>
internal sealed class UserEntityConfiguration : IEntityTypeConfiguration<UserEntity>
{
    /// <summary>Configures the <see cref="UserEntity"/> schema mapping.</summary>
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username).IsRequired().HasMaxLength(50);
        builder.Property(u => u.Role).IsRequired().HasMaxLength(50);
    }
}
