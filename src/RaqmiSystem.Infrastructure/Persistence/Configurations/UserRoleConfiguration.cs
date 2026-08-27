using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Infrastructure.Persistence.Configurations;

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles", "security");

        builder.HasKey(userRole => new { userRole.UserId, userRole.RoleId });

        builder.Property(userRole => userRole.UserId).HasColumnName("user_id");
        builder.Property(userRole => userRole.RoleId).HasColumnName("role_id");
        builder.Property(userRole => userRole.AssignedAt).HasColumnName("assigned_at");

        builder.HasOne(userRole => userRole.User)
            .WithMany(user => user.Roles)
            .HasForeignKey(userRole => userRole.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(userRole => userRole.Role)
            .WithMany()
            .HasForeignKey(userRole => userRole.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
