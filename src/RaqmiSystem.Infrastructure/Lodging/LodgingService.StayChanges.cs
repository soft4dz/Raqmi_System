using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Infrastructure.Persistence;
using System.Data;
using System.Globalization;

namespace RaqmiSystem.Infrastructure.Lodging;

/// <summary>
/// Les gestes qui changent CE QUI EST VENDU pendant un sejour : prolongation et changement de type
/// (surclassement ou declassement).
///
/// LES DEUX PARTAGENT UNE MEME DISCIPLINE. On ne retarife jamais une nuit DEJA POSEE : elle est
/// facturee, le client l'a vue, et la reecrire ferait apparaitre un prix different de celui
/// annonce. Seules les nuits a venir sont re-resolues, et l'ancien total reste au journal du
/// sejour - c'est ce qui permet de repondre plus tard a "pourquoi ce sejour ne coute plus le meme
/// prix qu'a la reservation".
/// </summary>
public sealed partial class LodgingService
{
    public async Task<ApplicationResult<ReservationResponse>> ExtendStayAsync(
        Guid id,
        ExtendStayRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var reservation = await dbContext.Set<Reservation>()
                .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

            if (reservation is null)
            {
                return ApplicationResult<ReservationResponse>.NotFound("Le dossier est introuvable.");
            }

            if (reservation.Status.IsClosed())
            {
                return ApplicationResult<ReservationResponse>.Conflict(
                    "Un sejour termine, annule ou en no-show ne peut plus etre modifie.");
            }

            if (request.DepartureDate == reservation.DepartureDate)
            {
                return ApplicationResult<ReservationResponse>.Validation(
                    "La date de depart demandee est deja celle du dossier.");
            }

            if (request.DepartureDate <= reservation.ArrivalDate)
            {
                return ApplicationResult<ReservationResponse>.Validation(
                    "La date de depart doit rester posterieure a la date d'arrivee.");
            }

            var now = DateTimeOffset.UtcNow;

            if (!await TryClaimReservationStatusAsync(reservation.Id, reservation.Status, now, cancellationToken))
            {
                return ApplicationResult<ReservationResponse>.Conflict(ConcurrentReservationMutationRefused);
            }

            var businessDay = await ResolveBusinessDateAsync(reservation.HotelUnitCode, cancellationToken);
            var policy = await GetPolicyEntityAsync(reservation.HotelUnitCode, cancellationToken);
            var previousDeparture = reservation.DepartureDate;
            var isExtension = request.DepartureDate > previousDeparture;

            // ON NE REVALIDE QUE CE QU'ON AJOUTE. Revalider tout le sejour ferait echouer une
            // prolongation parce que le dossier lui-meme occupe ses propres nuits - on l'exclut du
            // comptage, mais les nuits deja consommees n'ont de toute facon plus rien a prouver.
            if (isExtension)
            {
                var extraFrom = previousDeparture;
                var extraTo = request.DepartureDate;

                if (!request.OverrideRestrictions)
                {
                    var restrictions = await LoadRestrictionsAsync(
                        reservation.HotelUnitCode,
                        reservation.ArrivalDate,
                        extraTo,
                        cancellationToken);

                    var decision = RestrictionSet.Evaluate(
                        restrictions,
                        reservation.ArrivalDate,
                        extraTo,
                        businessDay.Date,
                        reservation.RoomTypeCode,
                        reservation.RatePlanCodeSnapshot,
                        reservation.ChannelCode);

                    if (!decision.IsAllowed)
                    {
                        return ApplicationResult<ReservationResponse>.Validation(decision.Describe());
                    }
                }

                var availability = await BuildRoomTypeAvailabilityAsync(
                    reservation.HotelUnitCode,
                    reservation.RoomTypeCode,
                    extraFrom,
                    extraTo,
                    policy,
                    excludeReservationId: reservation.Id,
                    cancellationToken);

                var capacity = AvailabilityCalculator.CapacityForPublicSale(
                    availability,
                    request.AllowOverbooking && policy.OverbookingEnabled);

                if (!capacity.CanSell)
                {
                    var bottleneck = capacity.BottleneckNight;

                    return ApplicationResult<ReservationResponse>.Conflict(
                        bottleneck is null
                            ? "Aucune chambre de ce type n'est disponible sur les nuits ajoutees."
                            : $"La nuit du {bottleneck.Night:dd/MM/yyyy} n'a plus de chambre de type "
                              + $"{reservation.RoomTypeCode} disponible : la prolongation est refusee.");
                }

                // La chambre AFFECTEE doit elle aussi rester libre : le type peut avoir de la place
                // ailleurs sans que ce soit dans cette chambre-la.
                if (reservation.RoomId is { } roomId)
                {
                    var check = await EnsureRoomIsFreeAsync(reservation, roomId, extraFrom, extraTo, cancellationToken);

                    if (check.Failure is not null)
                    {
                        return check.Failure;
                    }
                }

                if (capacity.NextSaleIsOverbooking)
                {
                    reservation.MarkAsOverbooking();
                }
            }

            if (!isExtension)
            {
                // RACCOURCIR UN SEJOUR NE DEFACTURE RIEN. Les nuits deja posees au folio sont
                // facturees et le client les a vues ; les effacer d'office reecrirait une note
                // deja presentee. On refuse donc, avec le geste a faire : un avoir ou un
                // ajustement, decide par quelqu'un, jamais par le systeme.
                var postedBeyond = await dbContext.Set<FolioCharge>()
                    .AsNoTracking()
                    .Join(
                        dbContext.Set<Folio>().AsNoTracking().Where(folio => folio.ReservationId == reservation.Id),
                        charge => charge.FolioId,
                        folio => folio.Id,
                        (charge, folio) => charge)
                    .Where(charge => charge.Kind == ChargeKind.Night && charge.ChargeDate >= request.DepartureDate)
                    .CountAsync(cancellationToken);

                if (postedBeyond > 0)
                {
                    return ApplicationResult<ReservationResponse>.Conflict(
                        $"{postedBeyond} nuitee(s) sont deja facturees au-dela du {request.DepartureDate:dd/MM/yyyy}. "
                        + "Passez un ajustement sur le folio avant de raccourcir le sejour.");
                }
            }

            var previousTotal = reservation.TotalStayAmount;

            try
            {
                reservation.Reschedule(reservation.ArrivalDate, request.DepartureDate);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return ApplicationResult<ReservationResponse>.Validation(ex.Message);
            }

            var repricing = await RepriceAsync(reservation, businessDay.Date, policy, cancellationToken);

            if (repricing is not null)
            {
                return repricing;
            }

            reservation.MarkUpdated(context.UserName, now);

            var summary = isExtension
                ? $"Sejour prolonge : depart {previousDeparture:dd/MM/yyyy} -> {request.DepartureDate:dd/MM/yyyy}."
                : $"Sejour raccourci : depart {previousDeparture:dd/MM/yyyy} -> {request.DepartureDate:dd/MM/yyyy}.";

            if (!string.IsNullOrWhiteSpace(request.Reason))
            {
                summary += $" {request.Reason}";
            }

            AddJournalEntry(
                reservation,
                ReservationEventKind.DatesChanged,
                summary,
                context,
                now,
                businessDay.Date,
                previousDeparture.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                request.DepartureDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

            AddJournalEntry(
                reservation,
                ReservationEventKind.RateChanged,
                $"Total du sejour : {previousTotal:0.00} -> {reservation.TotalStayAmount:0.00}.",
                context,
                now,
                businessDay.Date,
                previousTotal.ToString("0.00", CultureInfo.InvariantCulture),
                reservation.TotalStayAmount.ToString("0.00", CultureInfo.InvariantCulture));

            await WriteAuditAsync(
                "lodging.stay.extended",
                ReservationsEntity,
                reservation.Id,
                context,
                new
                {
                    reservation.Number,
                    reservation.HotelUnitCode,
                    PreviousDeparture = previousDeparture,
                    reservation.DepartureDate,
                    PreviousTotal = previousTotal,
                    reservation.TotalStayAmount,
                    request.Reason
                },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<ReservationResponse>.Success(
                Map(reservation, await LoadRoomNumberAsync(reservation.RoomId, cancellationToken)));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<ReservationResponse>.Conflict(ConcurrentReservationMutationRefused);
        }
    }

    // ============================ Surclassement et declassement ============================

    public async Task<ApplicationResult<ReservationResponse>> ChangeRoomTypeAsync(
        Guid id,
        ChangeRoomTypeRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ApplicationResult<ReservationResponse>.Validation(
                "Le motif du changement de type est obligatoire : un surclassement gratuit doit pouvoir "
                + "s'expliquer au controle de gestion.");
        }

        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var reservation = await dbContext.Set<Reservation>()
                .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

            if (reservation is null)
            {
                return ApplicationResult<ReservationResponse>.NotFound("Le dossier est introuvable.");
            }

            if (reservation.Status.IsClosed())
            {
                return ApplicationResult<ReservationResponse>.Conflict(
                    "Un sejour termine, annule ou en no-show ne peut plus changer de type.");
            }

            var targetCode = NormalizeCodeOrEmpty(request.RoomTypeCode);

            if (targetCode == reservation.RoomTypeCode)
            {
                return ApplicationResult<ReservationResponse>.Validation(
                    "Le sejour est deja vendu sur ce type de chambre.");
            }

            var currentType = await dbContext.Set<RoomType>()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    type => type.HotelUnitCode == reservation.HotelUnitCode
                        && type.Code == reservation.RoomTypeCode,
                    cancellationToken);

            var targetType = await dbContext.Set<RoomType>()
                .AsNoTracking()
                .Include(type => type.Beds)
                .SingleOrDefaultAsync(
                    type => type.HotelUnitCode == reservation.HotelUnitCode && type.Code == targetCode,
                    cancellationToken);

            if (targetType is null)
            {
                return ApplicationResult<ReservationResponse>.NotFound(
                    $"Le type de chambre '{targetCode}' est introuvable dans cette unite.");
            }

            if (!targetType.IsActive)
            {
                return ApplicationResult<ReservationResponse>.Validation(
                    $"Le type de chambre '{targetCode}' est inactif.");
            }

            if (!targetType.CanHost(reservation.Adults, reservation.Children, reservation.Infants))
            {
                return ApplicationResult<ReservationResponse>.Validation(
                    $"Le type '{targetCode}' ne peut pas accueillir {reservation.Adults} adulte(s), "
                    + $"{reservation.Children} enfant(s) et {reservation.Infants} bebe(s).");
            }

            // UN CLIENT DEJA INSTALLE NE PEUT PAS SE RETROUVER SANS CHAMBRE : le laisser sans
            // affectation le ferait disparaitre du plan alors qu'il dort dans une chambre bien
            // reelle. Le controle vient AVANT toute mutation - une transaction annulee laisse
            // malgre tout l'entite suivie modifiee en memoire, et un appelant qui reutilise son
            // contexte verrait un dossier a moitie change.
            if (request.TargetRoomId is null
                && reservation.Status == ReservationStatus.CheckedIn
                && reservation.HasRoom)
            {
                return ApplicationResult<ReservationResponse>.Validation(
                    "Ce sejour est en cours : indiquez la chambre du nouveau type vers laquelle "
                    + "deplacer le client.");
            }

            var now = DateTimeOffset.UtcNow;

            if (!await TryClaimReservationStatusAsync(reservation.Id, reservation.Status, now, cancellationToken))
            {
                return ApplicationResult<ReservationResponse>.Conflict(ConcurrentReservationMutationRefused);
            }

            var businessDay = await ResolveBusinessDateAsync(reservation.HotelUnitCode, cancellationToken);
            var policy = await GetPolicyEntityAsync(reservation.HotelUnitCode, cancellationToken);

            // LE SENS VIENT DU RANG, PAS DU LIBELLE. Sans echelle de gamme declaree, "Suite" et
            // "Double" ne sont que deux codes et le systeme ne peut pas dire lequel est une montee
            // en gamme - ce qui rendrait impossible de distinguer un geste commercial d'une
            // retrogradation subie.
            var currentRank = currentType?.Rank ?? 0;
            var isUpgrade = targetType.Rank > currentRank;
            var isSideways = targetType.Rank == currentRank;

            // La periode a revalider commence a la date metier pour un sejour en cours : les nuits
            // deja passees ont eu lieu dans l'ancien type.
            var from = reservation.Status == ReservationStatus.CheckedIn && businessDay.Date > reservation.ArrivalDate
                ? businessDay.Date
                : reservation.ArrivalDate;

            if (from >= reservation.DepartureDate)
            {
                return ApplicationResult<ReservationResponse>.Validation(
                    "Il ne reste aucune nuit a venir sur ce sejour : le changement de type n'aurait aucun effet.");
            }

            var availability = await BuildRoomTypeAvailabilityAsync(
                reservation.HotelUnitCode,
                targetCode,
                from,
                reservation.DepartureDate,
                policy,
                excludeReservationId: reservation.Id,
                cancellationToken);

            var capacity = AvailabilityCalculator.CapacityForPublicSale(
                availability,
                request.AllowOverbooking && policy.OverbookingEnabled);

            if (!capacity.CanSell)
            {
                return ApplicationResult<ReservationResponse>.Conflict(
                    $"Aucune chambre de type {targetCode} n'est disponible sur les nuits restantes.");
            }

            var previousType = reservation.RoomTypeCode;
            var previousTotal = reservation.TotalStayAmount;
            var previousRoomNumber = await LoadRoomNumberAsync(reservation.RoomId, cancellationToken);

            try
            {
                reservation.ChangeRoomType(targetCode);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return ApplicationResult<ReservationResponse>.Validation(ex.Message);
            }

            // La chambre affectee doit suivre le type : garder une double affectee a un sejour
            // vendu en suite rendrait l'inventaire faux des la nuit suivante.
            if (request.TargetRoomId is { } targetRoomId)
            {
                var check = await EnsureRoomIsFreeAsync(
                    reservation,
                    targetRoomId,
                    from,
                    reservation.DepartureDate,
                    cancellationToken);

                if (check.Failure is not null)
                {
                    return check.Failure;
                }

                if (check.Room!.RoomTypeCode != targetCode)
                {
                    return ApplicationResult<ReservationResponse>.Validation(
                        $"La chambre {check.Room.Number} est de type {check.Room.RoomTypeCode}, "
                        + $"pas {targetCode}.");
                }

                reservation.MoveToRoom(check.Room.Id);

                await ReleaseCurrentAssignmentAsync(reservation.Id, now, context, request.Reason, cancellationToken);

                dbContext.Set<StayRoomAssignment>().Add(new StayRoomAssignment(
                    reservation.Id,
                    check.Room.Id,
                    check.Room.Number,
                    check.Room.RoomTypeCode,
                    now,
                    context.UserName,
                    request.Reason));
            }
            else if (reservation.HasRoom)
            {
                // Avant l'arrivee, la chambre en place n'est plus du bon type : on la libere plutot
                // que de laisser une incoherence. Le dossier repasse en attente d'affectation, ce
                // qui est visible au comptoir.
                reservation.ReleaseRoom();
                await ReleaseCurrentAssignmentAsync(
                    reservation.Id,
                    now,
                    context,
                    "Chambre liberee : changement de type.",
                    cancellationToken);
            }

            // TARIFICATION. Un surclassement OFFERT garde le prix vendu : c'est tout l'objet du
            // geste commercial, et c'est pour cela que le type d'origine reste sur le dossier.
            if (request.Chargeable)
            {
                var repricing = await RepriceAsync(reservation, businessDay.Date, policy, cancellationToken);

                if (repricing is not null)
                {
                    return repricing;
                }
            }

            reservation.MarkUpdated(context.UserName, now);

            var kind = isUpgrade
                ? ReservationEventKind.Upgraded
                : isSideways
                    ? ReservationEventKind.Note
                    : ReservationEventKind.Downgraded;

            var label = isUpgrade ? "Surclassement" : isSideways ? "Changement de type" : "Declassement";
            var difference = reservation.TotalStayAmount - previousTotal;

            AddJournalEntry(
                reservation,
                kind,
                $"{label} {previousType} -> {targetCode} ({(request.Chargeable ? "facture" : "offert")}) : "
                + $"{request.Reason} Ecart tarifaire : {difference:+0.00;-0.00;0.00}.",
                context,
                now,
                businessDay.Date,
                previousType,
                targetCode);

            await WriteAuditAsync(
                isUpgrade ? "lodging.stay.upgraded" : "lodging.stay.downgraded",
                ReservationsEntity,
                reservation.Id,
                context,
                new
                {
                    reservation.Number,
                    reservation.HotelUnitCode,
                    PreviousType = previousType,
                    NewType = targetCode,
                    PreviousRoom = previousRoomNumber,
                    request.Chargeable,
                    PreviousTotal = previousTotal,
                    reservation.TotalStayAmount,
                    Difference = difference,
                    request.Reason
                },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<ReservationResponse>.Success(
                Map(reservation, await LoadRoomNumberAsync(reservation.RoomId, cancellationToken)));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<ReservationResponse>.Conflict(ConcurrentReservationMutationRefused);
        }
    }

    /// <summary>
    /// Repose les tarifs figes du dossier apres un changement de dates ou de type.
    ///
    /// LES NUITS DEJA POSEES NE SONT PAS RETARIFEES. Elles gardent leur montant fige : elles sont
    /// facturees, le client les a vues, et les reecrire ferait apparaitre sur la note un prix
    /// different de celui annonce. Seules les nuits a partir de la date metier sont re-resolues.
    /// </summary>
    private async Task<ApplicationResult<ReservationResponse>?> RepriceAsync(
        Reservation reservation,
        DateOnly businessDate,
        LodgingPolicy policy,
        CancellationToken cancellationToken)
    {
        var existing = reservation.GetNightlyRates().ToDictionary(rate => rate.Night);
        var pivot = businessDate > reservation.ArrivalDate ? businessDate : reservation.ArrivalDate;

        var occupancy = await GetUnitOccupancyByNightAsync(
            reservation.HotelUnitCode,
            reservation.ArrivalDate,
            reservation.DepartureDate,
            policy,
            cancellationToken);

        var yieldRules = await LoadYieldRulesAsync(
            reservation.HotelUnitCode,
            reservation.ArrivalDate,
            reservation.DepartureDate,
            cancellationToken);

        var rates = new List<ReservationNightRate>();

        for (var night = reservation.ArrivalDate; night < reservation.DepartureDate; night = night.AddDays(1))
        {
            if (night < pivot && existing.TryGetValue(night, out var frozen))
            {
                rates.Add(frozen);
                continue;
            }

            var pricing = await ResolveStayRatesAsync(
                reservation.HotelUnitCode,
                reservation.RoomTypeCode,
                night,
                night.AddDays(1),
                reservation.CustomerCode,
                null,
                occupancy,
                businessDate,
                yieldRules,
                cancellationToken);

            if (!pricing.HasRate)
            {
                return ApplicationResult<ReservationResponse>.Validation(
                    pricing.Issue ?? "Le tarif n'a pas pu etre resolu pour les nuits modifiees.");
            }

            rates.Add(pricing.FrozenRates[0]);
        }

        try
        {
            reservation.RepriceNightlyRates(rates);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ApplicationResult<ReservationResponse>.Validation(ex.Message);
        }

        return null;
    }
}
