using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Crm;

namespace RaqmiSystem.Infrastructure.Crm;

public sealed class LoyaltyTierConfiguration : IEntityTypeConfiguration<LoyaltyTier>
{
    public void Configure(EntityTypeBuilder<LoyaltyTier> builder)
    {
        builder.ToTable("loyalty_tiers", "crm", table =>
        {
            table.HasCheckConstraint("ck_loyalty_tiers_threshold", "points_threshold >= 0");
        });

        builder.HasKey(tier => tier.Id);

        builder.Property(tier => tier.Id).HasColumnName("id");
        builder.Property(tier => tier.CreatedAt).HasColumnName("created_at");
        builder.Property(tier => tier.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(tier => tier.UpdatedAt).HasColumnName("updated_at");
        builder.Property(tier => tier.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(tier => tier.Code)
            .HasColumnName("code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(tier => tier.Label)
            .HasColumnName("label")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(tier => tier.PointsThreshold).HasColumnName("points_threshold");

        builder.Property(tier => tier.Benefits)
            .HasColumnName("benefits")
            .HasMaxLength(600);

        builder.Property(tier => tier.IsActive).HasColumnName("is_active");

        builder.HasIndex(tier => tier.Code)
            .IsUnique()
            .HasDatabaseName("ux_loyalty_tiers_code");

        // Two active tiers opening at the same balance would make "the tier of a balance"
        // ambiguous, and the programme would show one or the other depending on the sort. The
        // uniqueness is on the threshold of the ACTIVE tiers only, so a retired tier can keep the
        // threshold its successor now uses.
        builder.HasIndex(tier => tier.PointsThreshold)
            .IsUnique()
            .HasFilter("is_active")
            .HasDatabaseName("ux_loyalty_tiers_points_threshold_active");
    }
}
