using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Infrastructure.HumanResources;

public sealed class AbsenceRequestConfiguration : IEntityTypeConfiguration<AbsenceRequest>
{
    public void Configure(EntityTypeBuilder<AbsenceRequest> builder)
    {
        builder.ToTable("absences", "hr");

        builder.HasKey(absence => absence.Id);

        builder.Property(absence => absence.Id).HasColumnName("id");
        builder.Property(absence => absence.CreatedAt).HasColumnName("created_at");
        builder.Property(absence => absence.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(absence => absence.UpdatedAt).HasColumnName("updated_at");
        builder.Property(absence => absence.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(absence => absence.EmployeeId).HasColumnName("employee_id");

        builder.Property(absence => absence.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(absence => absence.StartDate).HasColumnName("start_date");
        builder.Property(absence => absence.EndDate).HasColumnName("end_date");
        builder.Property(absence => absence.Reason).HasColumnName("reason").HasMaxLength(400);

        builder.Property(absence => absence.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(absence => absence.DecidedAt).HasColumnName("decided_at");
        builder.Property(absence => absence.DecidedBy).HasColumnName("decided_by").HasMaxLength(160);
        builder.Property(absence => absence.DecisionNote).HasColumnName("decision_note").HasMaxLength(400);

        // The pre-payroll run scans approved absences overlapping a month, so the range columns
        // lead the index.
        builder.HasIndex(absence => new { absence.EmployeeId, absence.StartDate, absence.EndDate })
            .HasDatabaseName("ix_hr_absences_employee_range");

        builder.HasIndex(absence => absence.Status)
            .HasDatabaseName("ix_hr_absences_status");

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(absence => absence.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
