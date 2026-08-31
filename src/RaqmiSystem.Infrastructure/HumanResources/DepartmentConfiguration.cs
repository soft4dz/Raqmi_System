using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Infrastructure.HumanResources;

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("departments", "hr");

        builder.HasKey(department => department.Id);

        builder.Property(department => department.Id).HasColumnName("id");
        builder.Property(department => department.CreatedAt).HasColumnName("created_at");
        builder.Property(department => department.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(department => department.UpdatedAt).HasColumnName("updated_at");
        builder.Property(department => department.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(department => department.Code)
            .HasColumnName("code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(department => department.Label)
            .HasColumnName("label")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(department => department.IsActive).HasColumnName("is_active");

        builder.HasIndex(department => department.Code)
            .IsUnique()
            .HasDatabaseName("ux_hr_departments_code");
    }
}
