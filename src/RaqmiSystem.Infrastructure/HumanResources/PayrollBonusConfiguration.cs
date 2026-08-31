using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Infrastructure.HumanResources;

public sealed class PayrollBonusConfiguration : IEntityTypeConfiguration<PayrollBonus>
{
    public void Configure(EntityTypeBuilder<PayrollBonus> builder)
    {
        builder.ToTable("payroll_bonuses", "hr");

        builder.HasKey(bonus => bonus.Id);

        builder.Property(bonus => bonus.Id).HasColumnName("id");
        builder.Property(bonus => bonus.CreatedAt).HasColumnName("created_at");
        builder.Property(bonus => bonus.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(bonus => bonus.UpdatedAt).HasColumnName("updated_at");
        builder.Property(bonus => bonus.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(bonus => bonus.Period)
            .HasColumnName("period")
            .HasConversion<PayrollMonthConverter>()
            .HasMaxLength(PayrollMonth.TextLength)
            .IsRequired();

        builder.Property(bonus => bonus.EmployeeId).HasColumnName("employee_id");

        builder.Property(bonus => bonus.Code)
            .HasColumnName("code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(bonus => bonus.Label)
            .HasColumnName("label")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(bonus => bonus.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2);

        // Several bonuses per employee and period are legitimate (attendance, night, exceptional),
        // so this index is deliberately NOT unique - it only serves the per-period lookup the
        // pre-payroll run does.
        builder.HasIndex(bonus => new { bonus.Period, bonus.EmployeeId })
            .HasDatabaseName("ix_hr_payroll_bonuses_period_employee");

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(bonus => bonus.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
