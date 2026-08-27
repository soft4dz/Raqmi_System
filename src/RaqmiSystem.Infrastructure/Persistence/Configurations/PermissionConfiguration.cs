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

        builder.Property(permission => permission.Id).HasColumnName("id");
        builder.Property(permission => permission.CreatedAt).HasColumnName("created_at");
        builder.Property(permission => permission.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(permission => permission.UpdatedAt).HasColumnName("updated_at");
        builder.Property(permission => permission.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(permission => permission.Key)
            .HasColumnName("key")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(permission => permission.Name)
            .HasColumnName("name")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(permission => permission.Category)
            .HasColumnName("category")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(permission => permission.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(permission => permission.IsSystem)
            .HasColumnName("is_system");

        builder.HasIndex(permission => permission.Key)
            .IsUnique();
    }
}
