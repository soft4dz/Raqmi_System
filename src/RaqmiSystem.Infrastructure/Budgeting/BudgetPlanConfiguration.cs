using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Budgeting;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Budgeting;

public sealed class BudgetPlanConfiguration : IEntityTypeConfiguration<BudgetPlan>
{
    public void Configure(EntityTypeBuilder<BudgetPlan> builder)
    {
        builder.ToTable("budget_plans", "budgeting", table =>
        {
            table.HasCheckConstraint(
                "ck_budget_plans_status",
                "status IN ('Draft', 'Approved', 'Closed')");

            table.HasCheckConstraint(
                "ck_budget_plans_year",
                "year BETWEEN 2000 AND 2999");
        });

        builder.HasKey(plan => plan.Id);

        builder.Property(plan => plan.Id).HasColumnName("id");
        builder.Property(plan => plan.CreatedAt).HasColumnName("created_at");
        builder.Property(plan => plan.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(plan => plan.UpdatedAt).HasColumnName("updated_at");
        builder.Property(plan => plan.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(plan => plan.Year)
            .HasColumnName("year");

        builder.Property(plan => plan.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(plan => plan.Label)
            .HasColumnName("label")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(plan => plan.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(plan => plan.ApprovedAt).HasColumnName("approved_at");
        builder.Property(plan => plan.ApprovedBy).HasColumnName("approved_by").HasMaxLength(160);
        builder.Property(plan => plan.ClosedAt).HasColumnName("closed_at");
        builder.Property(plan => plan.ClosedBy).HasColumnName("closed_by").HasMaxLength(160);

        builder.Ignore(plan => plan.CanEdit);
        builder.Ignore(plan => plan.TotalTarget);

        // A unit cannot be steered against two competing budgets for the same exercise. This is
        // also the concurrency guard behind the existence pre-check in BudgetService.CreatePlanAsync:
        // two concurrent creates for the same (year, unit) collide here and the loser gets a 409.
        builder.HasIndex(plan => new { plan.Year, plan.HotelUnitCode })
            .IsUnique()
            .HasDatabaseName("ux_budget_plans_year_hotel_unit");

        builder.HasIndex(plan => plan.Status)
            .HasDatabaseName("ix_budget_plans_status");

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(plan => plan.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(plan => plan.Lines)
            .WithOne()
            .HasForeignKey(line => line.BudgetPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        // Lines is an IReadOnlyCollection projection over the _lines field; EF must mutate the
        // field, never the projection.
        builder.Navigation(plan => plan.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
