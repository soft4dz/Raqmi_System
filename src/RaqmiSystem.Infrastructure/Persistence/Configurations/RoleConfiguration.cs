using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles", "security");

        builder.HasKey(role => role.Id);

        builder.Property(role => role.Name)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(role => role.DisplayName)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(role => role.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.HasIndex(role => role.Name)
            .IsUnique();

        builder.HasMany(role => role.Permissions)
            .WithOne(rolePermission => rolePermission.Role)
            .HasForeignKey(rolePermission => rolePermission.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
