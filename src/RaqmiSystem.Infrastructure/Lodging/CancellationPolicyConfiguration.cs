using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Lodging;

public sealed class CancellationPolicyConfiguration : IEntityTypeConfiguration<CancellationPolicy>
{
    public void Configure(EntityTypeBuilder<CancellationPolicy> builder)
    {
        builder.ToTable("cancellation_policies", "lodging", table =>
        {
            table.HasCheckConstraint(
                "ck_cancellation_policies_no_show_basis",
                "no_show_basis IN ('None', 'FirstNight', 'Nights', 'PercentOfStay', 'FixedAmount')");
        });

        builder.HasKey(policy => policy.Id);

        builder.Property(policy => policy.Id).HasColumnName("id");
        builder.Property(policy => policy.CreatedAt).HasColumnName("created_at");
        builder.Property(policy => policy.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(policy => policy.UpdatedAt).HasColumnName("updated_at");
        builder.Property(policy => policy.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(policy => policy.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(policy => policy.Code)
            .HasColumnName("code")
            .HasMaxLength(CancellationPolicy.CodeMaxLength)
            .IsRequired();

        builder.Property(policy => policy.Label)
            .HasColumnName("label")
            .HasMaxLength(CancellationPolicy.LabelMaxLength)
            .IsRequired();

        builder.Property(policy => policy.Description)
            .HasColumnName("description")
            .HasMaxLength(CancellationPolicy.DescriptionMaxLength);

        builder.Property(policy => policy.IsActive).HasColumnName("is_active");

        builder.Property(policy => policy.NoShowBasis)
            .HasColumnName("no_show_basis")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(policy => policy.NoShowValue).HasColumnName("no_show_value").HasPrecision(18, 2);

        builder.HasIndex(policy => new { policy.HotelUnitCode, policy.Code })
            .IsUnique()
            .HasDatabaseName("ux_cancellation_policies_hotel_unit_code_code");

        builder.HasMany(policy => policy.Rules)
            .WithOne()
            .HasForeignKey(rule => rule.CancellationPolicyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(policy => policy.Rules).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(policy => policy.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
