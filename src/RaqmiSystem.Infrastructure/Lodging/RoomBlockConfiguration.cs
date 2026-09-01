using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Lodging;

public sealed class RoomBlockConfiguration : IEntityTypeConfiguration<RoomBlock>
{
    public void Configure(EntityTypeBuilder<RoomBlock> builder)
    {
        builder.ToTable("room_blocks", "lodging", table =>
        {
            table.HasCheckConstraint(
                "ck_room_blocks_kind",
                "kind IN ('OutOfOrder', 'OutOfService')");

            table.HasCheckConstraint(
                "ck_room_blocks_status",
                "status IN ('Planned', 'Active', 'Closed', 'Cancelled')");

            // Au moins une nuit : la periode demi-ouverte doit etre non vide.
            table.HasCheckConstraint("ck_room_blocks_dates", "end_date > start_date");
        });

        builder.HasKey(block => block.Id);

        builder.Property(block => block.Id).HasColumnName("id");
        builder.Property(block => block.CreatedAt).HasColumnName("created_at");
        builder.Property(block => block.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(block => block.UpdatedAt).HasColumnName("updated_at");
        builder.Property(block => block.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(block => block.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(block => block.RoomId).HasColumnName("room_id");

        builder.Property(block => block.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(block => block.Category)
            .HasColumnName("category")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(block => block.StartDate).HasColumnName("start_date");
        builder.Property(block => block.EndDate).HasColumnName("end_date");
        builder.Property(block => block.ActualReturnDate).HasColumnName("actual_return_date");

        builder.Property(block => block.Reason)
            .HasColumnName("reason")
            .HasMaxLength(RoomBlock.ReasonMaxLength)
            .IsRequired();

        builder.Property(block => block.MaintenanceReference)
            .HasColumnName("maintenance_reference")
            .HasMaxLength(RoomBlock.MaintenanceReferenceMaxLength);

        builder.Property(block => block.Comment)
            .HasColumnName("comment")
            .HasMaxLength(RoomBlock.CommentMaxLength);

        builder.Property(block => block.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(block => block.ClosedAt).HasColumnName("closed_at");
        builder.Property(block => block.ClosedBy).HasColumnName("closed_by").HasMaxLength(160);

        builder.Property(block => block.CancelReason)
            .HasColumnName("cancel_reason")
            .HasMaxLength(RoomBlock.ReasonMaxLength);

        builder.Ignore(block => block.Nights);
        builder.Ignore(block => block.IsBlocking);

        // La recherche de disponibilite balaie les blocages d'une unite sur une fenetre de dates :
        // c'est l'acces le plus chaud de cette table.
        builder.HasIndex(block => new { block.HotelUnitCode, block.StartDate, block.EndDate })
            .HasDatabaseName("ix_room_blocks_unit_period");

        builder.HasIndex(block => new { block.RoomId, block.StartDate })
            .HasDatabaseName("ix_room_blocks_room_id_start_date");

        builder.HasIndex(block => block.Status)
            .HasDatabaseName("ix_room_blocks_status");

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(block => block.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(block => block.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
