using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", "security");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Id).HasColumnName("id");
        builder.Property(user => user.CreatedAt).HasColumnName("created_at");
        builder.Property(user => user.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(user => user.UpdatedAt).HasColumnName("updated_at");
        builder.Property(user => user.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(user => user.UserName)
            .HasColumnName("user_name")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(user => user.NormalizedUserName)
            .HasColumnName("normalized_user_name")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(user => user.Email)
            .HasColumnName("email")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(user => user.NormalizedEmail)
            .HasColumnName("normalized_email")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(user => user.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(user => user.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(user => user.IsActive)
            .HasColumnName("is_active");

        builder.Property(user => user.MustChangePassword)
            .HasColumnName("must_change_password");

        builder.Property(user => user.LastLoginAt)
            .HasColumnName("last_login_at");

        builder.HasIndex(user => user.NormalizedUserName)
            .IsUnique();

        builder.HasIndex(user => user.NormalizedEmail)
            .IsUnique();

        builder.HasMany(user => user.Roles)
            .WithOne(userRole => userRole.User)
            .HasForeignKey(userRole => userRole.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
