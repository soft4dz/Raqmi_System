using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Tariffs;

namespace RaqmiSystem.Infrastructure.Tariffs;

public sealed class RatePeriodConfiguration : IEntityTypeConfiguration<RatePeriod>
{
    public void Configure(EntityTypeBuilder<RatePeriod> builder)
    {
        builder.ToTable("rate_periods", "tariffs", table =>
        {
            // DateOnly is stored as a real date on PostgreSQL and as ISO-8601 TEXT
            // ("yyyy-MM-dd") on the SQLite test provider; ISO dates compare identically as
            // text and as dates, so the same constraint text holds on both.
            table.HasCheckConstraint(
                "ck_rate_periods_dates_ordered",
                "from_date <= to_date");

            // The CAST is not cosmetic: the SQLite provider used by the test harness stores
            // decimal as TEXT, and a text-versus-integer comparison there does not mean what
            // it says. Casting to numeric first makes the very same constraint text mean the
            // same thing on both providers - same pattern as BudgetLineConfiguration.
            table.HasCheckConstraint(
                "ck_rate_periods_nightly_amount_positive",
                "CAST(nightly_amount AS numeric) > 0");
        });

        builder.HasKey(period => period.Id);

        builder.Property(period => period.Id).HasColumnName("id");
        builder.Property(period => period.CreatedAt).HasColumnName("created_at");
        builder.Property(period => period.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(period => period.UpdatedAt).HasColumnName("updated_at");
        builder.Property(period => period.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(period => period.RatePlanId)
            .HasColumnName("rate_plan_id")
            .IsRequired();

        // Deliberately no foreign key: room types belong to the accommodation module, built in
        // parallel; the modules meet on the normalized code convention only (see the RatePeriod
        // entity's documentation).
        builder.Property(period => period.RoomTypeCode)
            .HasColumnName("room_type_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(period => period.FromDate)
            .HasColumnName("from_date");

        builder.Property(period => period.ToDate)
            .HasColumnName("to_date");

        builder.Property(period => period.NightlyAmount)
            .HasColumnName("nightly_amount")
            .HasPrecision(18, 2);

        // The overlap invariant itself cannot be a unique index (it is a range condition); it is
        // enforced by TariffService inside a Serializable transaction. This index is what both
        // the overlap check and resolution seek on.
        builder.HasIndex(period => new { period.RatePlanId, period.RoomTypeCode, period.FromDate })
            .HasDatabaseName("ix_rate_periods_plan_room_type_from_date");

        builder.HasOne<RatePlan>()
            .WithMany()
            .HasForeignKey(period => period.RatePlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
