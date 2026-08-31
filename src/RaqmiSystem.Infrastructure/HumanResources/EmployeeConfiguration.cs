using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.HumanResources;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.HumanResources;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("employees", "hr");

        builder.HasKey(employee => employee.Id);

        builder.Property(employee => employee.Id).HasColumnName("id");
        builder.Property(employee => employee.CreatedAt).HasColumnName("created_at");
        builder.Property(employee => employee.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(employee => employee.UpdatedAt).HasColumnName("updated_at");
        builder.Property(employee => employee.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(employee => employee.EmployeeNumber)
            .HasColumnName("employee_number")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(employee => employee.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(employee => employee.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(employee => employee.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(employee => employee.PositionCode)
            .HasColumnName("position_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(employee => employee.HireDate).HasColumnName("hire_date");
        builder.Property(employee => employee.TerminationDate).HasColumnName("termination_date");

        builder.Property(employee => employee.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(employee => employee.NationalIdentityNumber)
            .HasColumnName("national_identity_number")
            .HasMaxLength(40);

        builder.Property(employee => employee.SocialSecurityNumber)
            .HasColumnName("social_security_number")
            .HasMaxLength(40);

        builder.Property(employee => employee.BankAccountNumber)
            .HasColumnName("bank_account_number")
            .HasMaxLength(40);

        builder.Property(employee => employee.Email).HasColumnName("email").HasMaxLength(200);
        builder.Property(employee => employee.Phone).HasColumnName("phone").HasMaxLength(40);
        builder.Property(employee => employee.BadgeId).HasColumnName("badge_id").HasMaxLength(60);
        builder.Property(employee => employee.DependentChildren).HasColumnName("dependent_children");

        builder.HasIndex(employee => employee.EmployeeNumber)
            .IsUnique()
            .HasDatabaseName("ux_hr_employees_employee_number");

        // A badge identifies exactly one employee. Without this, two people could carry the same
        // badge and the time-clock import would attribute the same hours to both, or to whichever
        // row the query happened to return first. Filtered, because most employees have no badge
        // and NULL is not a value that should collide with itself.
        builder.HasIndex(employee => employee.BadgeId)
            .IsUnique()
            .HasFilter("badge_id IS NOT NULL")
            .HasDatabaseName("ux_hr_employees_badge_id");

        builder.HasIndex(employee => employee.HotelUnitCode)
            .HasDatabaseName("ix_hr_employees_hotel_unit_code");

        builder.HasIndex(employee => employee.PositionCode)
            .HasDatabaseName("ix_hr_employees_position_code");

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(employee => employee.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Position>()
            .WithMany()
            .HasPrincipalKey(position => position.Code)
            .HasForeignKey(employee => employee.PositionCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
