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

        builder.Property(role => role.Id).HasColumnName("id");
        builder.Property(role => role.CreatedAt).HasColumnName("created_at");
        builder.Property(role => role.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(role => role.UpdatedAt).HasColumnName("updated_at");
        builder.Property(role => role.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(role => role.Name)
            .HasColumnName("name")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(role => role.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(role => role.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(role => role.IsSystem)
            .HasColumnName("is_system");

        builder.Property(role => role.IsActive)
            .HasColumnName("is_active");

        builder.HasIndex(role => role.Name)
            .IsUnique();

        builder.HasMany(role => role.Permissions)
            .WithOne(rolePermission => rolePermission.Role)
            .HasForeignKey(rolePermission => rolePermission.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
