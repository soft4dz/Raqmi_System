using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Persistence.Configurations;

public sealed class HotelUnitConfiguration : IEntityTypeConfiguration<HotelUnit>
{
    public void Configure(EntityTypeBuilder<HotelUnit> builder)
    {
        builder.ToTable("hotel_units", "organization", table =>
        {
            table.HasCheckConstraint(
                "ck_hotel_units_display_order_non_negative",
                "display_order >= 0");

            table.HasCheckConstraint(
                "ck_hotel_units_unit_type",
                "unit_type IN ('Hotel', 'Residence', 'BeachClub', 'Marina', 'Other')");
        });

        builder.HasKey(unit => unit.Id);

        builder.Property(unit => unit.Id).HasColumnName("id");
        builder.Property(unit => unit.CreatedAt).HasColumnName("created_at");
        builder.Property(unit => unit.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(unit => unit.UpdatedAt).HasColumnName("updated_at");
        builder.Property(unit => unit.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(unit => unit.Code)
            .HasColumnName("code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(unit => unit.Name)
            .HasColumnName("name")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(unit => unit.UnitType)
            .HasColumnName("unit_type")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(unit => unit.DisplayOrder)
            .HasColumnName("display_order");

        builder.Property(unit => unit.IsActive)
            .HasColumnName("is_active");

        // No separate unique index on Code here: DailyRevenueConfiguration's
        // HasPrincipalKey(unit => unit.Code) already forces EF to create an alternate-key
        // unique constraint on this column (AK_hotel_units_code). Adding another explicit
        // HasIndex(...).IsUnique() would just duplicate that enforcement and its
        // index-maintenance cost on every insert/update.
        builder.HasIndex(unit => unit.DisplayOrder);
    }
}
