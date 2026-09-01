using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Lodging;

public sealed class RateRestrictionConfiguration : IEntityTypeConfiguration<RateRestriction>
{
    public void Configure(EntityTypeBuilder<RateRestriction> builder)
    {
        builder.ToTable("rate_restrictions", "lodging", table =>
        {
            table.HasCheckConstraint("ck_rate_restrictions_dates", "to_date >= from_date");

            table.HasCheckConstraint(
                "ck_rate_restrictions_stay_bounds",
                "minimum_stay >= 0 AND maximum_stay >= 0 "
                + "AND (minimum_stay = 0 OR maximum_stay = 0 OR minimum_stay <= maximum_stay)");

            table.HasCheckConstraint(
                "ck_rate_restrictions_advance_bounds",
                "min_advance_days >= 0 AND max_advance_days >= 0 "
                + "AND (min_advance_days = 0 OR max_advance_days = 0 OR min_advance_days <= max_advance_days)");
        });

        builder.HasKey(restriction => restriction.Id);

        builder.Property(restriction => restriction.Id).HasColumnName("id");
        builder.Property(restriction => restriction.CreatedAt).HasColumnName("created_at");
        builder.Property(restriction => restriction.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(restriction => restriction.UpdatedAt).HasColumnName("updated_at");
        builder.Property(restriction => restriction.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(restriction => restriction.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        // Portee : nulle signifie TOUS. Aucune cle etrangere sur ces trois colonnes - une
        // restriction peut viser un canal ou un plan qui n'existe pas encore dans le referentiel,
        // et elle ne fera simplement jamais match.
        builder.Property(restriction => restriction.RoomTypeCode)
            .HasColumnName("room_type_code")
            .HasMaxLength(40);

        builder.Property(restriction => restriction.RatePlanCode)
            .HasColumnName("rate_plan_code")
            .HasMaxLength(40);

        builder.Property(restriction => restriction.ChannelCode)
            .HasColumnName("channel_code")
            .HasMaxLength(40);

        builder.Property(restriction => restriction.FromDate).HasColumnName("from_date");
        builder.Property(restriction => restriction.ToDate).HasColumnName("to_date");

        builder.Property(restriction => restriction.IsClosed).HasColumnName("is_closed");
        builder.Property(restriction => restriction.IsClosedToArrival).HasColumnName("is_closed_to_arrival");
        builder.Property(restriction => restriction.IsClosedToDeparture).HasColumnName("is_closed_to_departure");
        builder.Property(restriction => restriction.MinimumStay).HasColumnName("minimum_stay");
        builder.Property(restriction => restriction.MaximumStay).HasColumnName("maximum_stay");
        builder.Property(restriction => restriction.MinAdvanceDays).HasColumnName("min_advance_days");
        builder.Property(restriction => restriction.MaxAdvanceDays).HasColumnName("max_advance_days");

        builder.Property(restriction => restriction.IsActive).HasColumnName("is_active");

        builder.Property(restriction => restriction.Notes)
            .HasColumnName("notes")
            .HasMaxLength(RateRestriction.NotesMaxLength);

        builder.Ignore(restriction => restriction.IsEmpty);

        // La recherche de disponibilite charge les restrictions d'une unite sur une fenetre : cet
        // index est celui qui evite un balayage complet a chaque devis.
        builder.HasIndex(restriction => new { restriction.HotelUnitCode, restriction.FromDate, restriction.ToDate })
            .HasDatabaseName("ix_rate_restrictions_unit_period");

        builder.HasIndex(restriction => new { restriction.HotelUnitCode, restriction.RoomTypeCode })
            .HasDatabaseName("ix_rate_restrictions_unit_room_type");

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(restriction => restriction.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
