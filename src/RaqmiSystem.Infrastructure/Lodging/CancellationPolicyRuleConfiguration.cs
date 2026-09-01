using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Infrastructure.Lodging;

public sealed class CancellationPolicyRuleConfiguration : IEntityTypeConfiguration<CancellationPolicyRule>
{
    public void Configure(EntityTypeBuilder<CancellationPolicyRule> builder)
    {
        builder.ToTable("cancellation_policy_rules", "lodging", table =>
        {
            table.HasCheckConstraint(
                "ck_cancellation_policy_rules_basis",
                "basis IN ('None', 'FirstNight', 'Nights', 'PercentOfStay', 'FixedAmount')");

            table.HasCheckConstraint(
                "ck_cancellation_policy_rules_days",
                "min_days_before_arrival >= 0 AND min_days_before_arrival <= 365");
        });

        builder.HasKey(rule => rule.Id);

        builder.Property(rule => rule.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(rule => rule.CancellationPolicyId).HasColumnName("cancellation_policy_id");
        builder.Property(rule => rule.MinDaysBeforeArrival).HasColumnName("min_days_before_arrival");

        builder.Property(rule => rule.Basis)
            .HasColumnName("basis")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(rule => rule.Value).HasColumnName("value").HasPrecision(18, 2);

        // Un seul palier par delai : deux paliers au meme J-N rendraient le bareme ambigu.
        builder.HasIndex(rule => new { rule.CancellationPolicyId, rule.MinDaysBeforeArrival })
            .IsUnique()
            .HasDatabaseName("ux_cancellation_policy_rules_policy_days");
    }
}
