using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Lodging;

public sealed class ExtraItemConfiguration : IEntityTypeConfiguration<ExtraItem>
{
    public void Configure(EntityTypeBuilder<ExtraItem> builder)
    {
        builder.ToTable("extra_items", "lodging", table =>
        {
            table.HasCheckConstraint(
                "ck_extra_items_pricing_basis",
                "pricing_basis IN ('PerStay', 'PerNight', 'PerPerson', 'PerPersonPerNight', 'PerQuantity')");

            table.HasCheckConstraint(
                "ck_extra_items_charge_kind",
                "charge_kind IN ('Extra', 'Tax', 'Package')");

            table.HasCheckConstraint(
                "ck_extra_items_vat_rate",
                "CAST(vat_rate AS numeric) IN (0, 9, 19)");

            table.HasCheckConstraint("ck_extra_items_unit_price", "CAST(unit_price AS numeric) >= 0");
        });

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at");
        builder.Property(item => item.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        builder.Property(item => item.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(item => item.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(item => item.Code)
            .HasColumnName("code")
            .HasMaxLength(ExtraItem.CodeMaxLength)
            .IsRequired();

        builder.Property(item => item.Label)
            .HasColumnName("label")
            .HasMaxLength(ExtraItem.LabelMaxLength)
            .IsRequired();

        builder.Property(item => item.Description)
            .HasColumnName("description")
            .HasMaxLength(ExtraItem.DescriptionMaxLength);

        builder.Property(item => item.PricingBasis)
            .HasColumnName("pricing_basis")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(item => item.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 2);
        builder.Property(item => item.VatRate).HasColumnName("vat_rate").HasPrecision(5, 2);

        builder.Property(item => item.ChargeKind)
            .HasColumnName("charge_kind")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(item => item.IsPostedByNightAudit).HasColumnName("is_posted_by_night_audit");
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.DisplayOrder).HasColumnName("display_order");

        // Le code d'extra est unique PAR UNITE, comme tout code de ce depot.
        builder.HasIndex(item => new { item.HotelUnitCode, item.Code })
            .IsUnique()
            .HasDatabaseName("ux_extra_items_hotel_unit_code_code");

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(item => item.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
