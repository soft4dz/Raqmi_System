using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Lodging;

public sealed class FolioConfiguration : IEntityTypeConfiguration<Folio>
{
    public void Configure(EntityTypeBuilder<Folio> builder)
    {
        builder.ToTable("folios", "lodging", table =>
        {
            table.HasCheckConstraint(
                "ck_folios_kind",
                "kind IN ('Guest', 'Company', 'Agency', 'Group')");

            table.HasCheckConstraint("ck_folios_status", "status IN ('Open', 'Closed')");
        });

        builder.HasKey(folio => folio.Id);

        builder.Property(folio => folio.Id).HasColumnName("id");
        builder.Property(folio => folio.CreatedAt).HasColumnName("created_at");
        builder.Property(folio => folio.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(folio => folio.UpdatedAt).HasColumnName("updated_at");
        builder.Property(folio => folio.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(folio => folio.ReservationId)
            .HasColumnName("reservation_id");

        builder.Property(folio => folio.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(folio => folio.Number)
            .HasColumnName("number")
            .HasMaxLength(Folio.NumberMaxLength)
            .IsRequired();

        builder.Property(folio => folio.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(folio => folio.BillToCustomerCode)
            .HasColumnName("bill_to_customer_code")
            .HasMaxLength(40);

        builder.Property(folio => folio.Label)
            .HasColumnName("label")
            .HasMaxLength(Folio.LabelMaxLength);

        builder.Property(folio => folio.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(folio => folio.ClosedAt).HasColumnName("closed_at");
        builder.Property(folio => folio.ClosedBy).HasColumnName("closed_by").HasMaxLength(160);
        builder.Property(folio => folio.InvoiceId).HasColumnName("invoice_id");

        builder.Ignore(folio => folio.Balance);
        builder.Ignore(folio => folio.TotalCharges);
        builder.Ignore(folio => folio.TotalSettlements);
        builder.Ignore(folio => folio.IsOpen);

        // L'unicite a bascule de la RESERVATION vers le NUMERO. Un sejour porte desormais
        // plusieurs folios - client, societe, agence - et l'ancien index unique sur
        // reservation_id l'interdisait. Ce qui doit rester unique, c'est le numero cite au
        // comptoir et repris sur la facture.
        builder.HasIndex(folio => new { folio.HotelUnitCode, folio.Number })
            .IsUnique()
            .HasDatabaseName("ux_folios_hotel_unit_code_number");

        builder.HasIndex(folio => folio.ReservationId)
            .HasDatabaseName("ix_folios_reservation_id");

        builder.HasOne<Reservation>()
            .WithMany()
            .HasForeignKey(folio => folio.ReservationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(folio => folio.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(folio => folio.Charges)
            .WithOne()
            .HasForeignKey(charge => charge.FolioId)
            .OnDelete(DeleteBehavior.Cascade);

        // Charges est une IReadOnlyCollection adossee au champ _charges ; EF doit ecrire dans le
        // champ, jamais dans la projection en lecture seule.
        builder.Navigation(folio => folio.Charges)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
