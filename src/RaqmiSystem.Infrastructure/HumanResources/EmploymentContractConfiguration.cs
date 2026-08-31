using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Infrastructure.HumanResources;

public sealed class EmploymentContractConfiguration : IEntityTypeConfiguration<EmploymentContract>
{
    public void Configure(EntityTypeBuilder<EmploymentContract> builder)
    {
        builder.ToTable("employment_contracts", "hr");

        builder.HasKey(contract => contract.Id);

        builder.Property(contract => contract.Id).HasColumnName("id");
        builder.Property(contract => contract.CreatedAt).HasColumnName("created_at");
        builder.Property(contract => contract.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(contract => contract.UpdatedAt).HasColumnName("updated_at");
        builder.Property(contract => contract.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(contract => contract.EmployeeId).HasColumnName("employee_id");

        builder.Property(contract => contract.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(contract => contract.StartDate).HasColumnName("start_date");
        builder.Property(contract => contract.EndDate).HasColumnName("end_date");

        builder.Property(contract => contract.GrossSalary)
            .HasColumnName("gross_salary")
            .HasPrecision(18, 2);

        builder.Property(contract => contract.WeeklyHours)
            .HasColumnName("weekly_hours")
            .HasPrecision(6, 2);

        builder.Property(contract => contract.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(contract => contract.TerminatedOn).HasColumnName("terminated_on");

        builder.Property(contract => contract.TerminationReason)
            .HasColumnName("termination_reason")
            .HasMaxLength(400);

        // THE invariant of the contract side: at most one ACTIVE contract per employee. The
        // pre-payroll run reads the contractual salary from that single row, so two active
        // contracts would not raise an error, they would silently pay whichever row came back
        // first. The service pre-checks for a friendly message; this filtered index is what makes
        // two concurrent creations impossible.
        builder.HasIndex(contract => contract.EmployeeId, "ux_hr_contracts_active_per_employee")
            .IsUnique()
            .HasFilter("status = 'Active'")
            .HasDatabaseName("ux_hr_contracts_active_per_employee");

        builder.HasIndex(contract => contract.EmployeeId, "ix_hr_contracts_employee_id")
            .HasDatabaseName("ix_hr_contracts_employee_id");

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(contract => contract.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
