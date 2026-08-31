using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Housekeeping;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Housekeeping;

public sealed class RoomConditionConfiguration : IEntityTypeConfiguration<RoomCondition>
{
    public void Configure(EntityTypeBuilder<RoomCondition> builder)
    {
        builder.ToTable("room_conditions", "housekeeping", table =>
        {
            table.HasCheckConstraint(
                "ck_room_conditions_status",
                "status IN ('Clean', 'Dirty', 'Inspected', 'OutOfOrder')");

            // The reason is what makes a withdrawal actionable; the domain demands it, and the
            // database refuses the pair the domain could never produce.
            table.HasCheckConstraint(
                "ck_room_conditions_out_of_order_reason",
                "(status <> 'OutOfOrder') OR (out_of_order_reason IS NOT NULL)");
        });

        builder.HasKey(condition => condition.Id);

        builder.Property(condition => condition.Id).HasColumnName("id");
        builder.Property(condition => condition.CreatedAt).HasColumnName("created_at");
        builder.Property(condition => condition.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(condition => condition.UpdatedAt).HasColumnName("updated_at");
        builder.Property(condition => condition.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(condition => condition.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(condition => condition.RoomId)
            .HasColumnName("room_id")
            .IsRequired();

        builder.Property(condition => condition.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(condition => condition.LastCleanedAt).HasColumnName("last_cleaned_at");
        builder.Property(condition => condition.LastCleanedBy).HasColumnName("last_cleaned_by").HasMaxLength(160);
        builder.Property(condition => condition.LastInspectedAt).HasColumnName("last_inspected_at");
        builder.Property(condition => condition.LastInspectedBy).HasColumnName("last_inspected_by").HasMaxLength(160);

        builder.Property(condition => condition.OutOfOrderReason)
            .HasColumnName("out_of_order_reason")
            .HasMaxLength(300);

        builder.Property(condition => condition.OutOfOrderUntil).HasColumnName("out_of_order_until");

        // At most ONE condition row per room: the row is the current state of that room, not a
        // history. Two concurrent first-declarations race past the service exists-check, collide
        // here, and the loser surfaces as a retryable 409 instead of a duplicated truth.
        builder.HasIndex(condition => condition.RoomId)
            .IsUnique()
            .HasDatabaseName("ux_room_conditions_room_id");

        // The board reads one unit at a time.
        builder.HasIndex(condition => condition.HotelUnitCode)
            .HasDatabaseName("ix_room_conditions_hotel_unit_code");

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(condition => condition.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(condition => condition.RoomId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
