using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Infrastructure.Persistence.Configurations;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions", "security");

        builder.HasKey(rolePermission => new { rolePermission.RoleId, rolePermission.PermissionId });

        builder.Property(rolePermission => rolePermission.RoleId).HasColumnName("role_id");
        builder.Property(rolePermission => rolePermission.PermissionId).HasColumnName("permission_id");
        builder.Property(rolePermission => rolePermission.GrantedAt).HasColumnName("granted_at");

        builder.HasOne(rolePermission => rolePermission.Role)
            .WithMany(role => role.Permissions)
            .HasForeignKey(rolePermission => rolePermission.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rolePermission => rolePermission.Permission)
            .WithMany()
            .HasForeignKey(rolePermission => rolePermission.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
