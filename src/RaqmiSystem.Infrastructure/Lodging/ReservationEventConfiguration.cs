using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Infrastructure.Lodging;

public sealed class ReservationEventConfiguration : IEntityTypeConfiguration<ReservationEvent>
{
    public void Configure(EntityTypeBuilder<ReservationEvent> builder)
    {
        builder.ToTable("reservation_events", "lodging");

        builder.HasKey(entry => entry.Id);

        // ValueGeneratedNever : l'entite s'attribue son Id, comme les autres lignes filles du
        // module. Sans cela une ligne ajoutee a une reservation deja suivie partirait en UPDATE.
        builder.Property(entry => entry.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(entry => entry.ReservationId).HasColumnName("reservation_id");

        builder.Property(entry => entry.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(entry => entry.Summary)
            .HasColumnName("summary")
            .HasMaxLength(ReservationEvent.SummaryMaxLength)
            .IsRequired();

        builder.Property(entry => entry.OccurredAt).HasColumnName("occurred_at");
        builder.Property(entry => entry.BusinessDate).HasColumnName("business_date");

        builder.Property(entry => entry.Actor)
            .HasColumnName("actor")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(entry => entry.PreviousValue)
            .HasColumnName("previous_value")
            .HasMaxLength(ReservationEvent.ValueMaxLength);

        builder.Property(entry => entry.NewValue)
            .HasColumnName("new_value")
            .HasMaxLength(ReservationEvent.ValueMaxLength);

        builder.HasIndex(entry => new { entry.ReservationId, entry.OccurredAt })
            .HasDatabaseName("ix_reservation_events_reservation_id_occurred_at");

        // Cascade assumee : le journal appartient au sejour. En pratique aucune reservation n'est
        // supprimee - le module travaille en suppression logique - mais la regle doit etre ecrite.
        builder.HasOne<Reservation>()
            .WithMany()
            .HasForeignKey(entry => entry.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
