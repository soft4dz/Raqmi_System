using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Infrastructure.Lodging;

public sealed class FolioConfiguration : IEntityTypeConfiguration<Folio>
{
    public void Configure(EntityTypeBuilder<Folio> builder)
    {
        builder.ToTable("folios", "lodging");

        builder.HasKey(folio => folio.Id);

        builder.Property(folio => folio.Id).HasColumnName("id");
        builder.Property(folio => folio.CreatedAt).HasColumnName("created_at");
        builder.Property(folio => folio.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(folio => folio.UpdatedAt).HasColumnName("updated_at");
        builder.Property(folio => folio.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(folio => folio.ReservationId)
            .HasColumnName("reservation_id");

        builder.Ignore(folio => folio.Balance);

        // Exactly one folio per stay: the backstop of the check-in claim, so even a race that
        // slipped past the conditional status claim could never open a second account.
        builder.HasIndex(folio => folio.ReservationId)
            .IsUnique()
            .HasDatabaseName("ux_folios_reservation_id");

        builder.HasOne<Reservation>()
            .WithMany()
            .HasForeignKey(folio => folio.ReservationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(folio => folio.Charges)
            .WithOne()
            .HasForeignKey(charge => charge.FolioId)
            .OnDelete(DeleteBehavior.Cascade);

        // Charges is an IReadOnlyCollection backed by the _charges field; EF must mutate the
        // field, never the read-only projection.
        builder.Navigation(folio => folio.Charges)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
