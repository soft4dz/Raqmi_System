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

        builder.Property(user => user.UserName)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(user => user.NormalizedUserName)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(user => user.Email)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(user => user.NormalizedEmail)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(user => user.DisplayName)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(user => user.PasswordHash)
            .HasMaxLength(500)
            .IsRequired();

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
