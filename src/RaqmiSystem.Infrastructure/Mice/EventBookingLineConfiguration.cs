using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Mice;

namespace RaqmiSystem.Infrastructure.Mice;

public sealed class EventBookingLineConfiguration : IEntityTypeConfiguration<EventBookingLine>
{
    public void Configure(EntityTypeBuilder<EventBookingLine> builder)
    {
        builder.ToTable("event_booking_lines", "lodging", table =>
        {
            // CAST en numeric : le fournisseur SQLite du harnais de test stocke un decimal en TEXT,
            // et une comparaison sans cast y devient lexicographique.
            table.HasCheckConstraint("ck_event_booking_lines_quantity", "CAST(quantity AS numeric) > 0");
            table.HasCheckConstraint("ck_event_booking_lines_unit_price", "CAST(unit_price AS numeric) >= 0");
        });

        builder.HasKey(line => line.Id);

        // ValueGeneratedNever est porteur : EventBookingLine s'attribue son propre Id, et une ligne
        // ajoutee a un evenement deja persiste serait sinon vue par la detection de changements avec
        // sa cle deja renseignee - ce qu'EF lit comme "cette ligne existe", donc marquee Modified et
        // suivie d'un UPDATE sur une ligne jamais inseree.
        builder.Property(line => line.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(line => line.EventBookingId).HasColumnName("event_booking_id");

        builder.Property(line => line.LineNumber).HasColumnName("line_number");

        builder.Property(line => line.Designation)
            .HasColumnName("designation")
            .HasMaxLength(EventBookingLine.DesignationMaxLength)
            .IsRequired();

        builder.Property(line => line.Quantity).HasColumnName("quantity").HasPrecision(12, 2);
        builder.Property(line => line.UnitPrice).HasColumnName("unit_price").HasPrecision(14, 2);
        builder.Property(line => line.VatRate).HasColumnName("vat_rate").HasPrecision(5, 2);

        // Totaux volontairement NON persistes : ils se deduisent de quantite, prix et taux, et une
        // colonne calculee stockee finirait par contredire ses composants.
        builder.Ignore(line => line.LineTotalExclVat);
        builder.Ignore(line => line.VatAmount);
        builder.Ignore(line => line.LineTotalInclVat);

        builder.HasIndex(line => line.EventBookingId, "ix_event_booking_lines_booking")
            .HasDatabaseName("ix_event_booking_lines_booking");
    }
}
