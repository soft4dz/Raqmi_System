using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Tariffs;

namespace RaqmiSystem.Infrastructure.Tariffs;

public sealed class CustomerConventionConfiguration : IEntityTypeConfiguration<CustomerConvention>
{
    public void Configure(EntityTypeBuilder<CustomerConvention> builder)
    {
        builder.ToTable("customer_conventions", "tariffs", table =>
        {
            // ISO dates compare identically as PostgreSQL dates and as SQLite TEXT - see
            // RatePeriodConfiguration for the same constraint.
            table.HasCheckConstraint(
                "ck_customer_conventions_dates_ordered",
                "from_date <= to_date");

            // CAST for the SQLite test provider's TEXT-stored decimals - same pattern as
            // BudgetLineConfiguration.
            table.HasCheckConstraint(
                "ck_customer_conventions_discount_percent_range",
                "discount_percent IS NULL OR " +
                "(CAST(discount_percent AS numeric) >= 0 AND CAST(discount_percent AS numeric) <= 100)");
        });

        builder.HasKey(convention => convention.Id);

        builder.Property(convention => convention.Id).HasColumnName("id");
        builder.Property(convention => convention.CreatedAt).HasColumnName("created_at");
        builder.Property(convention => convention.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(convention => convention.UpdatedAt).HasColumnName("updated_at");
        builder.Property(convention => convention.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(convention => convention.CustomerCode)
            .HasColumnName("customer_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(convention => convention.RatePlanCode)
            .HasColumnName("rate_plan_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(convention => convention.DiscountPercent)
            .HasColumnName("discount_percent")
            .HasPrecision(5, 2);

        builder.Property(convention => convention.FromDate)
            .HasColumnName("from_date");

        builder.Property(convention => convention.ToDate)
            .HasColumnName("to_date");

        builder.Property(convention => convention.IsActive)
            .HasColumnName("is_active");

        // The at-most-one-active-convention-per-customer-and-day invariant is a range condition
        // that no unique index can express; it is enforced by TariffService inside a Serializable
        // transaction. This index carries both that check and resolution's lookup.
        builder.HasIndex(convention => new { convention.CustomerCode, convention.FromDate })
            .HasDatabaseName("ix_customer_conventions_customer_from_date");

        builder.HasOne<Customer>()
            .WithMany()
            .HasPrincipalKey(customer => customer.Code)
            .HasForeignKey(convention => convention.CustomerCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<RatePlan>()
            .WithMany()
            .HasPrincipalKey(plan => plan.Code)
            .HasForeignKey(convention => convention.RatePlanCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
