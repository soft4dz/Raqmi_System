using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Infrastructure.HumanResources;

public sealed class PayslipConfiguration : IEntityTypeConfiguration<Payslip>
{
    public void Configure(EntityTypeBuilder<Payslip> builder)
    {
        builder.ToTable("payslips", "hr");

        builder.HasKey(payslip => payslip.Id);

        builder.Property(payslip => payslip.Id).HasColumnName("id");
        builder.Property(payslip => payslip.CreatedAt).HasColumnName("created_at");
        builder.Property(payslip => payslip.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(payslip => payslip.UpdatedAt).HasColumnName("updated_at");
        builder.Property(payslip => payslip.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(payslip => payslip.Period)
            .HasColumnName("period")
            .HasConversion<PayrollMonthConverter>()
            .HasMaxLength(PayrollMonth.TextLength)
            .IsRequired();

        builder.Property(payslip => payslip.EmployeeId).HasColumnName("employee_id");

        builder.Property(payslip => payslip.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(payslip => payslip.BaseGross)
            .HasColumnName("base_gross")
            .HasPrecision(18, 2);

        builder.Property(payslip => payslip.HoursWorked)
            .HasColumnName("hours_worked")
            .HasPrecision(8, 2);

        builder.Property(payslip => payslip.OvertimeHours)
            .HasColumnName("overtime_hours")
            .HasPrecision(8, 2);

        builder.Property(payslip => payslip.OvertimeAmount)
            .HasColumnName("overtime_amount")
            .HasPrecision(18, 2);

        builder.Property(payslip => payslip.UnpaidAbsenceDays)
            .HasColumnName("unpaid_absence_days")
            .HasPrecision(6, 2);

        builder.Property(payslip => payslip.AbsenceDeduction)
            .HasColumnName("absence_deduction")
            .HasPrecision(18, 2);

        builder.Property(payslip => payslip.BonusTotal)
            .HasColumnName("bonus_total")
            .HasPrecision(18, 2);

        builder.Property(payslip => payslip.TaxableGross)
            .HasColumnName("taxable_gross")
            .HasPrecision(18, 2);

        builder.Property(payslip => payslip.EmployeeSocialContribution)
            .HasColumnName("employee_social_contribution")
            .HasPrecision(18, 2);

        builder.Property(payslip => payslip.IncomeTaxBase)
            .HasColumnName("income_tax_base")
            .HasPrecision(18, 2);

        builder.Property(payslip => payslip.IncomeTax)
            .HasColumnName("income_tax")
            .HasPrecision(18, 2);

        builder.Property(payslip => payslip.NetPay)
            .HasColumnName("net_pay")
            .HasPrecision(18, 2);

        builder.Property(payslip => payslip.EmployerSocialContribution)
            .HasColumnName("employer_social_contribution")
            .HasPrecision(18, 2);

        builder.Property(payslip => payslip.EmployerWorkAccident)
            .HasColumnName("employer_work_accident")
            .HasPrecision(18, 2);

        builder.Property(payslip => payslip.EmployerUnemploymentInsurance)
            .HasColumnName("employer_unemployment_insurance")
            .HasPrecision(18, 2);

        builder.Property(payslip => payslip.EmployerVocationalTraining)
            .HasColumnName("employer_vocational_training")
            .HasPrecision(18, 2);

        builder.Property(payslip => payslip.EmployerPayrollTaxes)
            .HasColumnName("employer_payroll_taxes")
            .HasPrecision(18, 2);

        builder.Property(payslip => payslip.EmployerCost)
            .HasColumnName("employer_cost")
            .HasPrecision(18, 2);

        builder.Property(payslip => payslip.BelowMinimumWage).HasColumnName("below_minimum_wage");

        builder.Property(payslip => payslip.ValidatedAt).HasColumnName("validated_at");
        builder.Property(payslip => payslip.ValidatedBy).HasColumnName("validated_by").HasMaxLength(160);

        // One payslip per employee and period. This is what makes the pre-payroll run an upsert
        // rather than an append: re-running a month updates the drafts instead of stacking a
        // second payslip on top of the first.
        builder.HasIndex(payslip => new { payslip.Period, payslip.EmployeeId })
            .IsUnique()
            .HasDatabaseName("ux_hr_payslips_period_employee");

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(payslip => payslip.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
