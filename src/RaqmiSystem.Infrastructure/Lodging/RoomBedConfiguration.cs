using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Infrastructure.Lodging;

public sealed class RoomBedConfiguration : IEntityTypeConfiguration<RoomBed>
{
    public void Configure(EntityTypeBuilder<RoomBed> builder)
    {
        builder.ToTable("room_beds", "lodging", table =>
        {
            table.HasCheckConstraint("ck_room_beds_quantity", "quantity > 0");
        });

        builder.HasKey(bed => bed.Id);

        // Meme raison que pour RoomTypeBed : identifiant auto-attribue.
        builder.Property(bed => bed.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(bed => bed.RoomId).HasColumnName("room_id");

        builder.Property(bed => bed.BedType)
            .HasColumnName("bed_type")
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();

        builder.Property(bed => bed.Quantity).HasColumnName("quantity");

        builder.Ignore(bed => bed.Sleeps);

        builder.HasIndex(bed => new { bed.RoomId, bed.BedType }, "ux_room_beds_room_bed")
            .IsUnique()
            .HasDatabaseName("ux_room_beds_room_bed");
    }
}
