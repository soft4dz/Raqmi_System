using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Infrastructure.Lodging;

public sealed class RoomTypeBedConfiguration : IEntityTypeConfiguration<RoomTypeBed>
{
    public void Configure(EntityTypeBuilder<RoomTypeBed> builder)
    {
        builder.ToTable("room_type_beds", "lodging", table =>
        {
            table.HasCheckConstraint("ck_room_type_beds_quantity", "quantity > 0");
        });

        builder.HasKey(bed => bed.Id);

        // ValueGeneratedNever est porteur : RoomTypeBed s'attribue son propre Id, et une ligne
        // ajoutee a un type deja persiste serait sinon vue par EF avec sa cle deja renseignee -
        // donc marquee Modified, suivie d'un UPDATE sur une ligne jamais inseree.
        builder.Property(bed => bed.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(bed => bed.RoomTypeId).HasColumnName("room_type_id");

        builder.Property(bed => bed.BedType)
            .HasColumnName("bed_type")
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();

        builder.Property(bed => bed.Quantity).HasColumnName("quantity");

        // Total calcule a partir de la nature et de la quantite : jamais stocke, il finirait par
        // contredire ses composants.
        builder.Ignore(bed => bed.Sleeps);

        // Une nature de lit ne figure qu'UNE fois par type : "2 lits simples" est une ligne, pas
        // deux lignes de un.
        builder.HasIndex(bed => new { bed.RoomTypeId, bed.BedType }, "ux_room_type_beds_type_bed")
            .IsUnique()
            .HasDatabaseName("ux_room_type_beds_type_bed");
    }
}
