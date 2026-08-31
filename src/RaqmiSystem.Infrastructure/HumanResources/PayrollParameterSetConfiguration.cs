using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Infrastructure.HumanResources;

public sealed class PayrollParameterSetConfiguration : IEntityTypeConfiguration<PayrollParameterSet>
{
    public void Configure(EntityTypeBuilder<PayrollParameterSet> builder)
    {
        builder.ToTable("payroll_parameter_sets", "hr");

        builder.HasKey(set => set.Id);

        builder.Property(set => set.Id).HasColumnName("id");
        builder.Property(set => set.CreatedAt).HasColumnName("created_at");
        builder.Property(set => set.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(set => set.UpdatedAt).HasColumnName("updated_at");
        builder.Property(set => set.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(set => set.EffectiveFrom)
            .HasColumnName("effective_from")
            .HasConversion<PayrollMonthConverter>()
            .HasMaxLength(PayrollMonth.TextLength)
            .IsRequired();

        builder.Property(set => set.Label)
            .HasColumnName("label")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(set => set.MonthlyReferenceHours)
            .HasColumnName("monthly_reference_hours")
            .HasPrecision(8, 2);

        builder.Property(set => set.OvertimeMultiplier)
            .HasColumnName("overtime_multiplier")
            .HasPrecision(6, 3);

        builder.Property(set => set.ReferenceDaysPerMonth).HasColumnName("reference_days_per_month");

        // Rates are fractions with five decimals: 0.0125 (work accident) needs four, and the
        // fifth leaves room for a rate the finance act expresses in half basis points.
        builder.Property(set => set.EmployeeSocialRate)
            .HasColumnName("employee_social_rate")
            .HasPrecision(6, 5);

        builder.Property(set => set.EmployerSocialRate)
            .HasColumnName("employer_social_rate")
            .HasPrecision(6, 5);

        builder.Property(set => set.WorkAccidentRate)
            .HasColumnName("work_accident_rate")
            .HasPrecision(6, 5);

        builder.Property(set => set.UnemploymentInsuranceRate)
            .HasColumnName("unemployment_insurance_rate")
            .HasPrecision(6, 5);

        builder.Property(set => set.VocationalTrainingRate)
            .HasColumnName("vocational_training_rate")
            .HasPrecision(6, 5);

        builder.Property(set => set.IncomeTaxAbatement)
            .HasColumnName("income_tax_abatement")
            .HasPrecision(18, 2);

        builder.Property(set => set.IncomeTaxAbatementPerChild)
            .HasColumnName("income_tax_abatement_per_child")
            .HasPrecision(18, 2);

        builder.Property(set => set.MinimumWage)
            .HasColumnName("minimum_wage")
            .HasPrecision(18, 2);

        // One version per effective period: two sets starting the same month would make the
        // resolution "the most recent set at or before this period" ambiguous, and the payroll of
        // a whole month would depend on which row the database returned first.
        builder.HasIndex(set => set.EffectiveFrom)
            .IsUnique()
            .HasDatabaseName("ux_hr_payroll_parameter_sets_effective_from");

        // The scale is owned by its version: brackets have no life of their own, they are never
        // queried without their set, and replacing a scale replaces the whole collection.
        builder.OwnsMany(set => set.Brackets, brackets =>
        {
            brackets.ToTable("payroll_tax_brackets", "hr");

            brackets.WithOwner().HasForeignKey("parameter_set_id");

            brackets.Property<Guid>("id").HasColumnName("id");
            brackets.HasKey("id");

            brackets.Property(bracket => bracket.Ordinal).HasColumnName("ordinal");

            brackets.Property(bracket => bracket.UpperBound)
                .HasColumnName("upper_bound")
                .HasPrecision(18, 2);

            brackets.Property(bracket => bracket.Rate)
                .HasColumnName("rate")
                .HasPrecision(6, 5);

            brackets.HasIndex("parameter_set_id", "Ordinal")
                .IsUnique()
                .HasDatabaseName("ux_hr_payroll_tax_brackets_set_ordinal");
        });

        // The scale is useless without its brackets and every read of a set needs them, so they
        // are always loaded rather than left to each call site to remember.
        builder.Navigation(set => set.Brackets).AutoInclude();
    }
}
