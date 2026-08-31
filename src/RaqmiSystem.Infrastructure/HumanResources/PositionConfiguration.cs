using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Infrastructure.HumanResources;

public sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable("positions", "hr");

        builder.HasKey(position => position.Id);

        builder.Property(position => position.Id).HasColumnName("id");
        builder.Property(position => position.CreatedAt).HasColumnName("created_at");
        builder.Property(position => position.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(position => position.UpdatedAt).HasColumnName("updated_at");
        builder.Property(position => position.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(position => position.Code)
            .HasColumnName("code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(position => position.Label)
            .HasColumnName("label")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(position => position.DepartmentCode)
            .HasColumnName("department_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(position => position.MinimumGrossSalary)
            .HasColumnName("minimum_gross_salary")
            .HasPrecision(18, 2);

        builder.Property(position => position.IsActive).HasColumnName("is_active");

        builder.HasIndex(position => position.Code)
            .IsUnique()
            .HasDatabaseName("ux_hr_positions_code");

        builder.HasIndex(position => position.DepartmentCode)
            .HasDatabaseName("ix_hr_positions_department_code");

        builder.HasOne<Department>()
            .WithMany()
            .HasPrincipalKey(department => department.Code)
            .HasForeignKey(position => position.DepartmentCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
