using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Infrastructure.HumanResources;

public sealed class PayrollPeriodConfiguration : IEntityTypeConfiguration<PayrollPeriod>
{
    public void Configure(EntityTypeBuilder<PayrollPeriod> builder)
    {
        builder.ToTable("payroll_periods", "hr");

        builder.HasKey(period => period.Id);

        builder.Property(period => period.Id).HasColumnName("id");
        builder.Property(period => period.CreatedAt).HasColumnName("created_at");
        builder.Property(period => period.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(period => period.UpdatedAt).HasColumnName("updated_at");
        builder.Property(period => period.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(period => period.Period)
            .HasColumnName("period")
            .HasConversion<PayrollMonthConverter>()
            .HasMaxLength(PayrollMonth.TextLength)
            .IsRequired();

        builder.Property(period => period.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(period => period.PayslipCount).HasColumnName("payslip_count");
        builder.Property(period => period.ValidatedAt).HasColumnName("validated_at");
        builder.Property(period => period.ValidatedBy).HasColumnName("validated_by").HasMaxLength(160);
        builder.Property(period => period.ClosedAt).HasColumnName("closed_at");
        builder.Property(period => period.ClosedBy).HasColumnName("closed_by").HasMaxLength(160);

        // One row per period, and it is the row every write of the module claims before touching
        // anything else - see PayrollService. Two rows for the same month would mean two locks,
        // one of which could be open while the other is closed.
        builder.HasIndex(period => period.Period)
            .IsUnique()
            .HasDatabaseName("ux_hr_payroll_periods_period");
    }
}
