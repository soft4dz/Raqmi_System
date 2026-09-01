using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Infrastructure.Lodging;

public sealed class ReservationExtraConfiguration : IEntityTypeConfiguration<ReservationExtra>
{
    public void Configure(EntityTypeBuilder<ReservationExtra> builder)
    {
        builder.ToTable("reservation_extras", "lodging", table =>
        {
            table.HasCheckConstraint("ck_reservation_extras_quantity", "CAST(quantity AS numeric) > 0");

            table.HasCheckConstraint(
                "ck_reservation_extras_unit_price",
                "CAST(unit_price_snapshot AS numeric) >= 0");
        });

        builder.HasKey(extra => extra.Id);

        builder.Property(extra => extra.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(extra => extra.ReservationId).HasColumnName("reservation_id");

        builder.Property(extra => extra.ExtraCode)
            .HasColumnName("extra_code")
            .HasMaxLength(ExtraItem.CodeMaxLength)
            .IsRequired();

        builder.Property(extra => extra.LabelSnapshot)
            .HasColumnName("label_snapshot")
            .HasMaxLength(ReservationExtra.LabelMaxLength)
            .IsRequired();

        builder.Property(extra => extra.PricingBasis)
            .HasColumnName("pricing_basis")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(extra => extra.UnitPriceSnapshot)
            .HasColumnName("unit_price_snapshot")
            .HasPrecision(18, 2);

        builder.Property(extra => extra.VatRateSnapshot)
            .HasColumnName("vat_rate_snapshot")
            .HasPrecision(5, 2);

        builder.Property(extra => extra.ChargeKind)
            .HasColumnName("charge_kind")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(extra => extra.Quantity).HasColumnName("quantity").HasPrecision(12, 3);
        builder.Property(extra => extra.FromDate).HasColumnName("from_date");
        builder.Property(extra => extra.ToDate).HasColumnName("to_date");
        builder.Property(extra => extra.IsIncludedInRate).HasColumnName("is_included_in_rate");

        builder.Property(extra => extra.PackageCode)
            .HasColumnName("package_code")
            .HasMaxLength(Package.CodeMaxLength);

        builder.Property(extra => extra.Notes)
            .HasColumnName("notes")
            .HasMaxLength(ReservationExtra.NotesMaxLength);

        builder.HasIndex(extra => extra.ReservationId)
            .HasDatabaseName("ix_reservation_extras_reservation_id");

        builder.HasOne<Reservation>()
            .WithMany()
            .HasForeignKey(extra => extra.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
