using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Infrastructure.HumanResources;

public sealed class TimeEntryConfiguration : IEntityTypeConfiguration<TimeEntry>
{
    public void Configure(EntityTypeBuilder<TimeEntry> builder)
    {
        builder.ToTable("time_entries", "hr");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id).HasColumnName("id");
        builder.Property(entry => entry.CreatedAt).HasColumnName("created_at");
        builder.Property(entry => entry.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(entry => entry.UpdatedAt).HasColumnName("updated_at");
        builder.Property(entry => entry.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(entry => entry.EmployeeId).HasColumnName("employee_id");
        builder.Property(entry => entry.WorkDate).HasColumnName("work_date");

        builder.Property(entry => entry.HoursWorked)
            .HasColumnName("hours_worked")
            .HasPrecision(6, 2);

        builder.Property(entry => entry.Source)
            .HasColumnName("source")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(entry => entry.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(entry => entry.ValidatedAt).HasColumnName("validated_at");
        builder.Property(entry => entry.ValidatedBy).HasColumnName("validated_by").HasMaxLength(160);

        // One entry per employee and day. Two rows would be summed by the pre-payroll run and
        // double-count the overtime, which is why the upsert path exists in the service.
        builder.HasIndex(entry => new { entry.EmployeeId, entry.WorkDate })
            .IsUnique()
            .HasDatabaseName("ux_hr_time_entries_employee_date");

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(entry => entry.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
