using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Budgeting;

namespace RaqmiSystem.Infrastructure.Budgeting;

public sealed class BudgetLineConfiguration : IEntityTypeConfiguration<BudgetLine>
{
    public void Configure(EntityTypeBuilder<BudgetLine> builder)
    {
        builder.ToTable("budget_lines", "budgeting", table =>
        {
            table.HasCheckConstraint(
                "ck_budget_lines_month",
                "month BETWEEN 1 AND 12");

            table.HasCheckConstraint(
                "ck_budget_lines_category",
                "category IN ('Accommodation', 'Food', 'Beverage', 'Other')");

            // The CAST is not cosmetic: the SQLite provider used by the test harness stores
            // decimal as TEXT, and a text-versus-integer comparison there does not mean what it
            // says (SQLite orders every numeric value before every text value, so the bare
            // "amount_target >= 0" would be vacuously true and stop guarding anything). Casting
            // to numeric first makes the very same constraint text mean the same thing on both
            // providers - same reason as ApplicationSettingsConfiguration.
            table.HasCheckConstraint(
                "ck_budget_lines_amount_target_non_negative",
                "CAST(amount_target AS numeric) >= 0");
        });

        builder.HasKey(line => line.Id);

        // ValueGeneratedNever is load-bearing, not decoration. BudgetLine assigns its own Id
        // (Guid.NewGuid() on the property) so a line has a stable identity before it is ever
        // persisted. Left on EF's default convention for a Guid key (ValueGeneratedOnAdd), a line
        // added to an ALREADY-PERSISTED plan is discovered by change detection with its key
        // already set, which EF reads as "this row exists" - it tracks the line as Modified and
        // emits an UPDATE against a row that was never inserted, failing the whole SaveChanges
        // with a DbUpdateConcurrencyException ("expected to affect 1 row(s), but actually
        // affected 0"). Declaring the key as never store-generated tells EF the truth and makes
        // such a line track as Added. This costs no schema change: the column carries no database
        // default either way, since the value has always come from the application.
        builder.Property(line => line.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(line => line.BudgetPlanId)
            .HasColumnName("budget_plan_id")
            .IsRequired();

        builder.Property(line => line.Month)
            .HasColumnName("month");

        builder.Property(line => line.Category)
            .HasColumnName("category")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(line => line.AmountTarget)
            .HasColumnName("amount_target")
            .HasPrecision(18, 2);

        // One target per plan, month and category: the pair (month, category) is the line's
        // business key, and BudgetPlan.SetLine / ReplaceLines adjust the existing row rather than
        // adding a second, contradictory target.
        builder.HasIndex(line => new { line.BudgetPlanId, line.Month, line.Category })
            .IsUnique()
            .HasDatabaseName("ux_budget_lines_plan_month_category");
    }
}
