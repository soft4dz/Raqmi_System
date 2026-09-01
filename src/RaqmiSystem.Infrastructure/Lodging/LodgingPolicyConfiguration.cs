using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Lodging;

public sealed class LodgingPolicyConfiguration : IEntityTypeConfiguration<LodgingPolicy>
{
    public void Configure(EntityTypeBuilder<LodgingPolicy> builder)
    {
        builder.ToTable("lodging_policies", "lodging", table =>
        {
            table.HasCheckConstraint(
                "ck_lodging_policies_early_percent",
                "CAST(early_check_in_percent_of_night AS numeric) BETWEEN 0 AND 100");

            table.HasCheckConstraint(
                "ck_lodging_policies_late_percent",
                "CAST(late_check_out_percent_of_night AS numeric) BETWEEN 0 AND 100");
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

        builder.Property(policy => policy.CheckInTime).HasColumnName("check_in_time");
        builder.Property(policy => policy.CheckOutTime).HasColumnName("check_out_time");

        builder.Property(policy => policy.EarlyCheckInFromTime).HasColumnName("early_check_in_from_time");
        builder.Property(policy => policy.EarlyCheckInIsFree).HasColumnName("early_check_in_is_free");
        builder.Property(policy => policy.EarlyCheckInFlatCharge)
            .HasColumnName("early_check_in_flat_charge")
            .HasPrecision(18, 2);
        builder.Property(policy => policy.EarlyCheckInPercentOfNight)
            .HasColumnName("early_check_in_percent_of_night")
            .HasPrecision(5, 2);

        builder.Property(policy => policy.LateCheckOutUntilTime).HasColumnName("late_check_out_until_time");
        builder.Property(policy => policy.LateCheckOutIsFree).HasColumnName("late_check_out_is_free");
        builder.Property(policy => policy.LateCheckOutFlatCharge)
            .HasColumnName("late_check_out_flat_charge")
            .HasPrecision(18, 2);
        builder.Property(policy => policy.LateCheckOutPercentOfNight)
            .HasColumnName("late_check_out_percent_of_night")
            .HasPrecision(5, 2);

        builder.Property(policy => policy.OutOfServiceReducesInventory)
            .HasColumnName("out_of_service_reduces_inventory");

        builder.Property(policy => policy.OverbookingEnabled).HasColumnName("overbooking_enabled");

        // Une seule politique par unite : deux lignes voudraient dire deux heures de check-in.
        builder.HasIndex(policy => policy.HotelUnitCode)
            .IsUnique()
            .HasDatabaseName("ux_lodging_policies_hotel_unit_code");

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(policy => policy.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
