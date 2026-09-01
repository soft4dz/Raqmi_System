using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Lodging;

public sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("reservations", "lodging", table =>
        {
            table.HasCheckConstraint(
                "ck_reservations_status",
                "status IN ('Inquiry', 'Option', 'Confirmed', 'Guaranteed', 'CheckedIn', "
                + "'CheckedOut', 'Cancelled', 'NoShow')");

            // Au moins une nuit : la periode demi-ouverte [arrivee, depart) doit etre non vide.
            table.HasCheckConstraint(
                "ck_reservations_dates",
                "departure_date > arrival_date");

            table.HasCheckConstraint(
                "ck_reservations_guest_count",
                "guest_count > 0 AND adults > 0 AND children >= 0 AND infants >= 0 "
                + "AND guest_count = adults + children");

            // CAST parce que le fournisseur SQLite des tests stocke les decimaux en TEXT (meme
            // technique que les contraintes de montant de la tresorerie et de la facturation).
            table.HasCheckConstraint(
                "ck_reservations_nightly_rate",
                "CAST(nightly_rate_snapshot AS numeric) >= 0");

            table.HasCheckConstraint(
                "ck_reservations_cancellation_fee",
                "CAST(cancellation_fee_amount AS numeric) >= 0");
        });

        builder.HasKey(reservation => reservation.Id);

        builder.Property(reservation => reservation.Id).HasColumnName("id");
        builder.Property(reservation => reservation.CreatedAt).HasColumnName("created_at");
        builder.Property(reservation => reservation.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(reservation => reservation.UpdatedAt).HasColumnName("updated_at");
        builder.Property(reservation => reservation.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(reservation => reservation.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(reservation => reservation.Number)
            .HasColumnName("number")
            .HasMaxLength(Reservation.NumberMaxLength)
            .IsRequired();

        // Le type est OBLIGATOIRE, la chambre est FACULTATIVE : c'est le fait central du modele.
        // Un client achete "une double standard", pas la 214 ; la chambre physique est affectee
        // quand l'hotel le decide, parfois seulement au comptoir.
        builder.Property(reservation => reservation.RoomTypeCode)
            .HasColumnName("room_type_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(reservation => reservation.OriginalRoomTypeCode)
            .HasColumnName("original_room_type_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(reservation => reservation.RoomId)
            .HasColumnName("room_id");

        builder.Property(reservation => reservation.CustomerCode)
            .HasColumnName("customer_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(reservation => reservation.ArrivalDate).HasColumnName("arrival_date");
        builder.Property(reservation => reservation.DepartureDate).HasColumnName("departure_date");
        builder.Property(reservation => reservation.EstimatedArrivalTime).HasColumnName("estimated_arrival_time");
        builder.Property(reservation => reservation.EstimatedDepartureTime).HasColumnName("estimated_departure_time");

        builder.Property(reservation => reservation.GuestCount).HasColumnName("guest_count");
        builder.Property(reservation => reservation.Adults).HasColumnName("adults");
        builder.Property(reservation => reservation.Children).HasColumnName("children");
        builder.Property(reservation => reservation.Infants).HasColumnName("infants");

        builder.Property(reservation => reservation.MarketSegmentCode)
            .HasColumnName("market_segment_code")
            .HasMaxLength(40);

        builder.Property(reservation => reservation.ChannelCode).HasColumnName("channel_code").HasMaxLength(40);
        builder.Property(reservation => reservation.SourceCode).HasColumnName("source_code").HasMaxLength(40);
        builder.Property(reservation => reservation.CompanyCode).HasColumnName("company_code").HasMaxLength(40);
        builder.Property(reservation => reservation.AgencyCode).HasColumnName("agency_code").HasMaxLength(40);
        builder.Property(reservation => reservation.ConventionCode).HasColumnName("convention_code").HasMaxLength(40);

        builder.Property(reservation => reservation.IsWalkIn).HasColumnName("is_walk_in");
        builder.Property(reservation => reservation.IsOverbooking).HasColumnName("is_overbooking");

        builder.Property(reservation => reservation.Notes)
            .HasColumnName("notes")
            .HasMaxLength(Reservation.NotesMaxLength);

        builder.Property(reservation => reservation.SpecialRequests)
            .HasColumnName("special_requests")
            .HasMaxLength(Reservation.SpecialRequestsMaxLength);

        builder.Property(reservation => reservation.Guarantee)
            .HasColumnName("guarantee_kind")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(reservation => reservation.GuaranteeReference)
            .HasColumnName("guarantee_reference")
            .HasMaxLength(Reservation.GuaranteeReferenceMaxLength);

        builder.Property(reservation => reservation.CancellationPolicyCode)
            .HasColumnName("cancellation_policy_code")
            .HasMaxLength(40);

        builder.Property(reservation => reservation.CancellationPolicySnapshotJson)
            .HasColumnName("cancellation_policy_snapshot");

        builder.Property(reservation => reservation.CancellationFeeAmount)
            .HasColumnName("cancellation_fee_amount")
            .HasPrecision(18, 2);

        builder.Property(reservation => reservation.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(reservation => reservation.NightlyRateSnapshot)
            .HasColumnName("nightly_rate_snapshot")
            .HasPrecision(18, 2);

        builder.Property(reservation => reservation.RatePlanCodeSnapshot)
            .HasColumnName("rate_plan_code_snapshot")
            .HasMaxLength(60)
            .IsRequired();

        // Detail des tarifs figes nuit par nuit (tableau JSON, une entree par nuit), ecrit a la
        // vente et reecrit uniquement par un geste explicite. Nullable : les lignes creees avant
        // l'existence de cette colonne continuent de facturer nightly_rate_snapshot a plat, ce sur
        // quoi GetNightlyRates retombe.
        builder.Property(reservation => reservation.NightlyRatesSnapshotJson)
            .HasColumnName("nightly_rates_snapshot");

        builder.Property(reservation => reservation.CancelReason)
            .HasColumnName("cancel_reason")
            .HasMaxLength(500);

        builder.Property(reservation => reservation.CheckedInAt).HasColumnName("checked_in_at");
        builder.Property(reservation => reservation.CheckedInBy).HasColumnName("checked_in_by").HasMaxLength(160);
        builder.Property(reservation => reservation.CheckedOutAt).HasColumnName("checked_out_at");
        builder.Property(reservation => reservation.CheckedOutBy).HasColumnName("checked_out_by").HasMaxLength(160);
        builder.Property(reservation => reservation.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(reservation => reservation.CancelledBy).HasColumnName("cancelled_by").HasMaxLength(160);
        builder.Property(reservation => reservation.NoShowAt).HasColumnName("no_show_at");
        builder.Property(reservation => reservation.NoShowBy).HasColumnName("no_show_by").HasMaxLength(160);

        builder.Ignore(reservation => reservation.Nights);
        builder.Ignore(reservation => reservation.IsBlocking);
        builder.Ignore(reservation => reservation.TotalStayAmount);
        builder.Ignore(reservation => reservation.HasRoom);

        // Le numero de dossier est unique PAR UNITE : c'est ce que le client cite au telephone.
        builder.HasIndex(reservation => new { reservation.HotelUnitCode, reservation.Number })
            .IsUnique()
            .HasDatabaseName("ux_reservations_hotel_unit_code_number");

        // La garde anti-double-reservation balaie les reservations d'UNE chambre sur une periode.
        builder.HasIndex(reservation => new { reservation.RoomId, reservation.ArrivalDate })
            .HasDatabaseName("ix_reservations_room_id_arrival_date");

        // La disponibilite PAR TYPE compte les nuitees vendues d'un type sur une fenetre : c'est
        // le chemin le plus chaud du moteur depuis que la vente se fait par type et non par
        // chambre. Sans cet index, chaque devis balaierait toutes les reservations de l'unite.
        builder.HasIndex(reservation => new
            {
                reservation.HotelUnitCode,
                reservation.RoomTypeCode,
                reservation.ArrivalDate,
                reservation.DepartureDate
            })
            .HasDatabaseName("ix_reservations_unit_type_period");

        builder.HasIndex(reservation => reservation.HotelUnitCode)
            .HasDatabaseName("ix_reservations_hotel_unit_code");

        builder.HasIndex(reservation => reservation.Status)
            .HasDatabaseName("ix_reservations_status");

        builder.HasIndex(reservation => reservation.CustomerCode)
            .HasDatabaseName("ix_reservations_customer_code");

        // Les tableaux d'arrivees et de departs du jour filtrent sur ces deux dates, unite par
        // unite : ce sont les deux ecrans les plus ouverts d'une reception.
        builder.HasIndex(reservation => new { reservation.HotelUnitCode, reservation.ArrivalDate })
            .HasDatabaseName("ix_reservations_unit_arrival_date");

        builder.HasIndex(reservation => new { reservation.HotelUnitCode, reservation.DepartureDate })
            .HasDatabaseName("ix_reservations_unit_departure_date");

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(reservation => reservation.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(reservation => reservation.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Customer>()
            .WithMany()
            .HasPrincipalKey(customer => customer.Code)
            .HasForeignKey(reservation => reservation.CustomerCode)
            .OnDelete(DeleteBehavior.Restrict);

        // Cle composee vers room_types : un dossier ne peut viser qu'un type de SON unite, meme
        // apres un surclassement. C'est la meme garantie que celle qui lie une chambre a son type.
        builder.HasOne<RoomType>()
            .WithMany()
            .HasPrincipalKey(roomType => new { roomType.HotelUnitCode, roomType.Code })
            .HasForeignKey(reservation => new { reservation.HotelUnitCode, reservation.RoomTypeCode })
            .OnDelete(DeleteBehavior.Restrict);

        // Rattachement au bloc de groupe. Nullable : la vente publique n'en a pas. Aucune cle
        // etrangere declaree vers room_allotments a dessein - un allotement annule ne doit pas
        // pouvoir effacer en cascade des reservations bien reelles.
        builder.Property(reservation => reservation.AllotmentId).HasColumnName("allotment_id");

        builder.Property(reservation => reservation.GuestName)
            .HasColumnName("guest_name")
            .HasMaxLength(160);

        builder.HasIndex(reservation => reservation.AllotmentId, "ix_reservations_allotment_id")
            .HasDatabaseName("ix_reservations_allotment_id");
    }
}
