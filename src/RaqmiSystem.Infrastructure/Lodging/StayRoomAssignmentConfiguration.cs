using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Infrastructure.Lodging;

public sealed class StayRoomAssignmentConfiguration : IEntityTypeConfiguration<StayRoomAssignment>
{
    public void Configure(EntityTypeBuilder<StayRoomAssignment> builder)
    {
        builder.ToTable("stay_room_assignments", "lodging");

        builder.HasKey(assignment => assignment.Id);

        builder.Property(assignment => assignment.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(assignment => assignment.ReservationId).HasColumnName("reservation_id");
        builder.Property(assignment => assignment.RoomId).HasColumnName("room_id");

        builder.Property(assignment => assignment.RoomNumber)
            .HasColumnName("room_number")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(assignment => assignment.RoomTypeCode)
            .HasColumnName("room_type_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(assignment => assignment.AssignedAt).HasColumnName("assigned_at");
        builder.Property(assignment => assignment.AssignedBy).HasColumnName("assigned_by").HasMaxLength(160);
        builder.Property(assignment => assignment.ReleasedAt).HasColumnName("released_at");
        builder.Property(assignment => assignment.ReleasedBy).HasColumnName("released_by").HasMaxLength(160);

        builder.Property(assignment => assignment.Reason)
            .HasColumnName("reason")
            .HasMaxLength(StayRoomAssignment.ReasonMaxLength);

        builder.Ignore(assignment => assignment.IsCurrent);

        builder.HasIndex(assignment => new { assignment.ReservationId, assignment.AssignedAt })
            .HasDatabaseName("ix_stay_room_assignments_reservation_id_assigned_at");

        builder.HasIndex(assignment => assignment.RoomId)
            .HasDatabaseName("ix_stay_room_assignments_room_id");

        builder.HasOne<Reservation>()
            .WithMany()
            .HasForeignKey(assignment => assignment.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Pas de suppression en cascade depuis la chambre : l'historique d'un sejour survit a la
        // sortie de parc d'une chambre, sans quoi on perdrait la trace de qui a dormi ou.
        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(assignment => assignment.RoomId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
