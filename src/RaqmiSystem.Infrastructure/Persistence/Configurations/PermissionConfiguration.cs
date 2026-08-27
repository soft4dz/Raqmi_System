using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Infrastructure.Persistence.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions", "security");

        builder.HasKey(permission => permission.Id);

        builder.Property(permission => permission.Key)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(permission => permission.Name)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(permission => permission.Category)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(permission => permission.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.HasIndex(permission => permission.Key)
            .IsUnique();
    }
}
