using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Mice;
using RaqmiSystem.Domain.Tariffs;
using RaqmiSystem.Infrastructure.Persistence;
using System.Data;

namespace RaqmiSystem.Infrastructure.Lodging;

/// <summary>
/// Le cycle de vie d'un dossier : creation, walk-in, statuts commerciaux, arrivee, depart,
/// annulation et no-show.
/// </summary>
public sealed partial class LodgingService
{
    public async Task<IReadOnlyCollection<ReservationResponse>> ListReservationsAsync(
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        ReservationStatus? status,
        string? customerCode,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<Reservation>().AsNoTracking();

        // Semantique du chevauchement : un sejour est liste des qu'il touche la fenetre [from, to],
        // de sorte qu'un client deja present avant le debut de la fenetre y figure encore.
        if (from.HasValue)
        {
            query = query.Where(reservation => reservation.DepartureDate > from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(reservation => reservation.ArrivalDate <= to.Value);
        }

        var normalizedUnitCode = NormalizeNullableCode(hotelUnitCode);

        if (normalizedUnitCode is not null)
        {
            query = query.Where(reservation => reservation.HotelUnitCode == normalizedUnitCode);
        }

        if (status.HasValue)
        {
            query = query.Where(reservation => reservation.Status == status.Value);
        }

        var normalizedCustomerCode = NormalizeNullableCode(customerCode);

        if (normalizedCustomerCode is not null)
        {
            query = query.Where(reservation => reservation.CustomerCode == normalizedCustomerCode);
        }

        var reservations = await query
            .OrderBy(reservation => reservation.ArrivalDate)
            .ThenBy(reservation => reservation.HotelUnitCode)
            .ThenBy(reservation => reservation.Number)
            .ToArrayAsync(cancellationToken);

        var roomNumbers = await LoadRoomNumbersAsync(
            reservations.Where(reservation => reservation.RoomId is not null)
                .Select(reservation => reservation.RoomId!.Value)
                .Distinct()
                .ToArray(),
            cancellationToken);

        var customerNames = await LoadCustomerNamesAsync(
            reservations.Select(reservation => reservation.CustomerCode).ToArray(),
            cancellationToken);

        return reservations
            .Select(reservation => Map(
                reservation,
                reservation.RoomId is { } roomId ? roomNumbers.GetValueOrDefault(roomId) : null,
                customerNames.GetValueOrDefault(reservation.CustomerCode)))
            .ToArray();
    }

    public async Task<ApplicationResult<ReservationResponse>> GetReservationAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var reservation = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (reservation is null)
        {
            return ApplicationResult<ReservationResponse>.NotFound("Le dossier est introuvable.");
        }

        var customerNames = await LoadCustomerNamesAsync([reservation.CustomerCode], cancellationToken);

        return ApplicationResult<ReservationResponse>.Success(Map(
            reservation,
            await LoadRoomNumberAsync(reservation.RoomId, cancellationToken),
            customerNames.GetValueOrDefault(reservation.CustomerCode)));
    }

    public async Task<ApplicationResult<ReservationDetailResponse>> GetReservationDetailAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var reservation = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (reservation is null)
        {
            return ApplicationResult<ReservationDetailResponse>.NotFound("Le dossier est introuvable.");
        }

        var folios = await dbContext.Set<Folio>()
            .AsNoTracking()
            .Include(folio => folio.Charges)
            .Where(folio => folio.ReservationId == id)
            .OrderBy(folio => folio.Number)
            .ToArrayAsync(cancellationToken);

        var extras = await dbContext.Set<ReservationExtra>()
            .AsNoTracking()
            .Where(extra => extra.ReservationId == id)
            .ToArrayAsync(cancellationToken);

        var deposits = await dbContext.Set<Deposit>()
            .AsNoTracking()
            .Where(deposit => deposit.ReservationId == id)
            .OrderBy(deposit => deposit.DueDate)
            .ToArrayAsync(cancellationToken);

        // Le tri se fait EN MEMOIRE : SQLite - le fournisseur des tests d'integration - ne sait pas
        // trier sur un DateTimeOffset, la ou PostgreSQL le fait sans peine. Les deux collections
        // sont bornees par sejour, l'ecart de cout est nul, et le comportement reste identique sur
        // les deux moteurs, ce qui est la seule chose qui compte ici.
        var assignments = (await dbContext.Set<StayRoomAssignment>()
            .AsNoTracking()
            .Where(assignment => assignment.ReservationId == id)
            .ToArrayAsync(cancellationToken))
            .OrderBy(assignment => assignment.AssignedAt)
            .ToArray();

        var journal = (await dbContext.Set<ReservationEvent>()
            .AsNoTracking()
            .Where(entry => entry.ReservationId == id)
            .ToArrayAsync(cancellationToken))
            .OrderByDescending(entry => entry.OccurredAt)
            .Take(200)
            .ToArray();

        var customerNames = await LoadCustomerNamesAsync([reservation.CustomerCode], cancellationToken);

        return ApplicationResult<ReservationDetailResponse>.Success(new ReservationDetailResponse(
            Map(
                reservation,
                await LoadRoomNumberAsync(reservation.RoomId, cancellationToken),
                customerNames.GetValueOrDefault(reservation.CustomerCode)),
            folios.Select(Map).ToArray(),
            extras.Select(extra => Map(extra, reservation)).ToArray(),
            deposits.Select(Map).ToArray(),
            assignments.Select(Map).ToArray(),
            journal.Select(Map).ToArray(),
            folios.Sum(folio => folio.Balance)));
    }

    // ===================================== Creation =====================================

    public async Task<ApplicationResult<ReservationResponse>> CreateReservationAsync(
        CreateReservationRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return await CreateReservationCoreAsync(request, context, isWalkIn: false, cancellationToken);
    }

    public async Task<ApplicationResult<ReservationResponse>> CreateWalkInAsync(
        WalkInRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var normalizedUnitCode = NormalizeCodeOrEmpty(request.HotelUnitCode);

        // La date d'arrivee d'un walk-in n'est pas une donnee de saisie : le client est LA. On la
        // prend a la date metier de l'unite, celle qui commande toute la journee d'exploitation.
        var businessDay = await ResolveBusinessDateAsync(normalizedUnitCode, cancellationToken);
        var arrival = businessDay.HasClosing ? businessDay.Date : today;

        if (request.DepartureDate <= arrival)
        {
            return ApplicationResult<ReservationResponse>.Validation(
                $"Le depart doit etre posterieur a la date d'arrivee ({arrival:dd/MM/yyyy}).");
        }

        var creation = await CreateReservationCoreAsync(
            new CreateReservationRequest(
                request.HotelUnitCode,
                request.RoomId,
                request.CustomerCode,
                arrival,
                request.DepartureDate,
                request.Adults + request.Children,
                AllotmentId: null,
                GuestName: request.GuestName,
                RoomTypeCode: null,
                Adults: request.Adults,
                Children: request.Children,
                Infants: request.Infants,
                EstimatedArrivalTime: TimeOnly.FromDateTime(DateTime.UtcNow),
                EstimatedDepartureTime: null,
                Status: ReservationStatus.Confirmed,
                MarketSegmentCode: request.MarketSegmentCode,
                ChannelCode: request.ChannelCode ?? "COMPTOIR",
                SourceCode: request.SourceCode,
                CompanyCode: null,
                AgencyCode: null,
                Notes: request.Notes,
                SpecialRequests: request.SpecialRequests,
                Guarantee: request.Guarantee,
                GuaranteeReference: request.GuaranteeReference,
                CancellationPolicyCode: null,
                AllowOverbooking: request.AllowOverbooking,
                OverrideRestrictions: request.OverrideRestrictions),
            context,
            isWalkIn: true,
            cancellationToken);

        if (!creation.Succeeded || creation.Value is null)
        {
            return creation;
        }

        // VENTE ET ARRIVEE SONT UN SEUL GESTE. Si l'arrivee echoue, le dossier cree resterait un
        // fantome sur une chambre que le client occupe pourtant : on annule alors le dossier plutot
        // que de le laisser en attente d'un check-in que personne ne fera.
        var checkIn = await CheckInAsync(creation.Value.Id, context, cancellationToken);

        if (checkIn.Succeeded)
        {
            return checkIn;
        }

        await CancelReservationAsync(
            creation.Value.Id,
            new CancelReservationRequest(
                "Walk-in interrompu : l'arrivee a echoue, le dossier est annule automatiquement."),
            context,
            cancellationToken);

        return ApplicationResult<ReservationResponse>.Validation(
            "Le walk-in a echoue a l'enregistrement de l'arrivee : "
            + (checkIn.Error ?? "raison inconnue.")
            + " Le dossier cree a ete annule.");
    }

    /// <summary>
    /// Le chemin unique de creation d'un dossier, walk-in compris.
    ///
    /// ORDRE DES CONTROLES, ET POURQUOI IL EST CELUI-LA. D'abord ce qui ne depend de personne
    /// d'autre (unite, client, type, composition) ; ensuite les restrictions de vente, qui peuvent
    /// fermer la periode sans qu'aucune chambre ne soit en cause ; ensuite la tarification, parce
    /// qu'un dossier sans prix n'existe pas ; et SEULEMENT ALORS, dans une transaction
    /// Serializable, la disponibilite et l'ecriture. Tout ce qui est cher et sans effet de bord se
    /// fait hors transaction, pour que le verrou dure le moins longtemps possible.
    /// </summary>
    private async Task<ApplicationResult<ReservationResponse>> CreateReservationCoreAsync(
        CreateReservationRequest request,
        OperationContext context,
        bool isWalkIn,
        CancellationToken cancellationToken)
    {
        if (request.DepartureDate <= request.ArrivalDate)
        {
            return ApplicationResult<ReservationResponse>.Validation(
                "La date de depart doit etre posterieure a la date d'arrivee (un sejour couvre au moins une nuit).");
        }

        if (request.DepartureDate.DayNumber - request.ArrivalDate.DayNumber > MaxAvailabilityWindowNights)
        {
            return ApplicationResult<ReservationResponse>.Validation(
                $"Un sejour ne peut pas depasser {MaxAvailabilityWindowNights} nuits en une seule reservation.");
        }

        var unitFailure = await RequireActiveHotelUnitAsync<ReservationResponse>(
            request.HotelUnitCode,
            cancellationToken);

        if (unitFailure.Failure is not null)
        {
            return unitFailure.Failure;
        }

        var unitCode = unitFailure.UnitCode;

        // Composition : les adultes sont deduits du total historique quand le detail n'est pas
        // fourni, ce qui garde compatibles les appels qui ne connaissent que GuestCount.
        var adults = request.Adults ?? Math.Max(1, request.GuestCount - request.Children);
        var children = request.Children;
        var infants = request.Infants;

        Room? room = null;

        if (request.RoomId is { } roomId)
        {
            room = await dbContext.Set<Room>()
                .AsNoTracking()
                .SingleOrDefaultAsync(current => current.Id == roomId, cancellationToken);

            if (room is null || room.HotelUnitCode != unitCode)
            {
                return ApplicationResult<ReservationResponse>.NotFound(
                    "La chambre est introuvable dans cette unite.");
            }

            if (!room.IsActive)
            {
                return ApplicationResult<ReservationResponse>.Validation(
                    "Aucune reservation ne peut etre prise sur une chambre inactive.");
            }
        }

        // Le type vendu : celui demande, ou celui de la chambre affectee. Une chambre passee SANS
        // type reste le chemin compatible avec les appels existants.
        var roomTypeCode = NormalizeNullableCode(request.RoomTypeCode) ?? room?.RoomTypeCode;

        if (roomTypeCode is null)
        {
            return ApplicationResult<ReservationResponse>.Validation(
                "Le type de chambre est requis : c'est ce que le client achete. Indiquez un type ou une chambre.");
        }

        if (room is not null && room.RoomTypeCode != roomTypeCode)
        {
            return ApplicationResult<ReservationResponse>.Validation(
                $"La chambre {room.Number} est de type {room.RoomTypeCode}, pas {roomTypeCode}. "
                + "Choisissez une chambre du type vendu, ou vendez le type de cette chambre.");
        }

        var roomType = await dbContext.Set<RoomType>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                current => current.HotelUnitCode == unitCode && current.Code == roomTypeCode,
                cancellationToken);

        if (roomType is null)
        {
            return ApplicationResult<ReservationResponse>.NotFound(
                $"Le type de chambre '{roomTypeCode}' est introuvable dans cette unite.");
        }

        if (!roomType.IsActive)
        {
            return ApplicationResult<ReservationResponse>.Validation(
                $"Le type de chambre '{roomTypeCode}' est inactif : il n'est plus vendable.");
        }

        if (!roomType.CanHost(adults, children, infants))
        {
            return ApplicationResult<ReservationResponse>.Validation(
                $"La composition demandee ({adults} adulte(s), {children} enfant(s), {infants} bebe(s)) "
                + $"depasse ce que le type '{roomType.Code}' peut accueillir "
                + $"({roomType.MaxOccupancy} occupant(s), {roomType.MaxCots} berceau(x)).");
        }

        var normalizedCustomerCode = NormalizeCodeOrEmpty(request.CustomerCode);

        var customer = await dbContext.Set<Customer>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == normalizedCustomerCode, cancellationToken);

        if (customer is null)
        {
            return ApplicationResult<ReservationResponse>.NotFound("Le client est introuvable.");
        }

        if (!customer.IsActive)
        {
            return ApplicationResult<ReservationResponse>.Validation(
                "Aucune reservation ne peut etre prise pour un client inactif.");
        }

        var policy = await GetPolicyEntityAsync(unitCode, cancellationToken);
        var businessDay = await ResolveBusinessDateAsync(unitCode, cancellationToken);

        // RESTRICTIONS DE VENTE. Elles se controlent avant la tarification : refuser une vente
        // fermee coute une requete, la tarifer d'abord en coute une par nuit.
        if (!request.OverrideRestrictions)
        {
            var restrictions = await LoadRestrictionsAsync(
                unitCode,
                request.ArrivalDate,
                request.DepartureDate,
                cancellationToken);

            var decision = RestrictionSet.Evaluate(
                restrictions,
                request.ArrivalDate,
                request.DepartureDate,
                businessDay.Date,
                roomTypeCode,
                null,
                NormalizeNullableCode(request.ChannelCode));

            if (!decision.IsAllowed)
            {
                return ApplicationResult<ReservationResponse>.Validation(decision.Describe());
            }
        }

        // TARIFICATION. Le tarif de chaque nuit est resolu puis FIGE : le folio facturera
        // exactement ce que la recherche a annonce, y compris a cheval sur deux periodes.
        var occupancy = await GetUnitOccupancyByNightAsync(
            unitCode,
            request.ArrivalDate,
            request.DepartureDate,
            policy,
            cancellationToken);

        var yieldRules = await LoadYieldRulesAsync(
            unitCode,
            request.ArrivalDate,
            request.DepartureDate,
            cancellationToken);

        var pricing = await ResolveStayRatesAsync(
            unitCode,
            roomTypeCode,
            request.ArrivalDate,
            request.DepartureDate,
            normalizedCustomerCode,
            null,
            occupancy,
            businessDay.Date,
            yieldRules,
            cancellationToken);

        if (!pricing.HasRate)
        {
            return ApplicationResult<ReservationResponse>.Validation(
                pricing.Issue ?? "Le tarif n'a pas pu etre resolu.");
        }

        var ratePlan = await dbContext.Set<RatePlan>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                plan => plan.HotelUnitCode == unitCode && plan.Code == pricing.RatePlanCode,
                cancellationToken);

        // Un plan qui EXIGE une garantie ne se vend pas sans : c'est tout l'objet d'un tarif non
        // remboursable ou d'un tarif negocie avec depot.
        if (ratePlan is { RequiredGuarantee: not GuaranteeKind.None }
            && request.Guarantee == GuaranteeKind.None)
        {
            return ApplicationResult<ReservationResponse>.Validation(
                $"Le plan tarifaire '{ratePlan.Code}' exige une garantie ({ratePlan.RequiredGuarantee}). "
                + "Renseignez-la avant de confirmer.");
        }

        var cancellationPolicy = await ResolveCancellationPolicyAsync(
            unitCode,
            NormalizeNullableCode(request.CancellationPolicyCode) ?? ratePlan?.CancellationPolicyCode,
            cancellationToken);

        // ================= Transaction : disponibilite + ecriture, indissociables =================
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var availability = await BuildRoomTypeAvailabilityAsync(
                unitCode,
                roomTypeCode,
                request.ArrivalDate,
                request.DepartureDate,
                policy,
                excludeReservationId: null,
                cancellationToken);

            var isOverbooking = false;

            if (request.AllotmentId is { } allotmentId)
            {
                // VENTE SUR BLOC : elle consomme l'allotement, qui avait deja retire la chambre de
                // la vente publique. On raisonne donc sur le disponible PHYSIQUE, pas sur le
                // disponible public - sinon le bloc serait compte deux fois et l'hotel
                // s'interdirait de vendre des chambres pourtant libres.
                var physical = availability.Nights.Min(night => night.PhysicalAvailable);

                if (physical < 1)
                {
                    return ApplicationResult<ReservationResponse>.Conflict(
                        $"Aucune chambre physique de type {roomTypeCode} n'est libre sur toute la periode.");
                }

                var blockFailure = await EnsureBlockHasRoomAsync(
                    unitCode,
                    roomTypeCode,
                    allotmentId,
                    request.ArrivalDate,
                    request.DepartureDate,
                    cancellationToken);

                if (blockFailure is not null)
                {
                    return blockFailure;
                }
            }
            else
            {
                var capacity = AvailabilityCalculator.CapacityForPublicSale(
                    availability,
                    request.AllowOverbooking && policy.OverbookingEnabled);

                if (!capacity.CanSell)
                {
                    var bottleneck = capacity.BottleneckNight;

                    return ApplicationResult<ReservationResponse>.Conflict(
                        bottleneck is null
                            ? $"Aucune chambre de type {roomTypeCode} n'est disponible sur cette periode."
                            : $"Aucune chambre de type {roomTypeCode} n'est disponible la nuit du "
                              + $"{bottleneck.Night:dd/MM/yyyy} : {bottleneck.SellableCapacity} exploitable(s), "
                              + $"{bottleneck.SoldRooms} vendue(s), {bottleneck.AllotmentHolds} tenue(s) pour un groupe.");
                }

                isOverbooking = capacity.NextSaleIsOverbooking;
            }

            // Chambre nommee : la garde anti-double-reservation, rejouee DANS la transaction.
            if (room is not null)
            {
                var overlapping = await dbContext.Set<Reservation>()
                    .Where(current => current.RoomId == room.Id)
                    .Where(BlocksPeriod(request.ArrivalDate, request.DepartureDate))
                    .AnyAsync(cancellationToken);

                if (overlapping)
                {
                    return ApplicationResult<ReservationResponse>.Conflict(RoomAlreadyReserved);
                }

                var blocked = await dbContext.Set<RoomBlock>()
                    .Where(block => block.RoomId == room.Id
                        && (block.Status == RoomBlockStatus.Planned || block.Status == RoomBlockStatus.Active)
                        && block.StartDate < request.DepartureDate
                        && block.EndDate > request.ArrivalDate)
                    .AnyAsync(cancellationToken);

                if (blocked)
                {
                    return ApplicationResult<ReservationResponse>.Conflict(
                        $"La chambre {room.Number} est hors service sur tout ou partie de cette periode.");
                }
            }

            Reservation reservation;

            try
            {
                reservation = new Reservation(
                    unitCode,
                    await AllocateReservationNumberAsync(unitCode, cancellationToken),
                    roomTypeCode,
                    room?.Id,
                    normalizedCustomerCode,
                    request.ArrivalDate,
                    request.DepartureDate,
                    adults,
                    pricing.FrozenRates[0].Amount,
                    pricing.FrozenRates[0].RatePlanCode,
                    children,
                    infants);

                reservation.FreezeNightlyRates(pricing.FrozenRates);
                reservation.SetSchedule(request.EstimatedArrivalTime, request.EstimatedDepartureTime);
                reservation.SetCommercialContext(
                    request.MarketSegmentCode,
                    request.ChannelCode,
                    request.SourceCode,
                    request.CompanyCode,
                    request.AgencyCode,
                    pricing.ConventionCustomerCode);
                reservation.SetNotes(request.Notes, request.SpecialRequests);
                reservation.SetGuarantee(request.Guarantee, request.GuaranteeReference);
                reservation.SetGuestName(request.GuestName);

                if (request.Status != ReservationStatus.Confirmed)
                {
                    reservation.MoveToPreArrivalStatus(request.Status);
                }

                if (cancellationPolicy is not null)
                {
                    reservation.FreezeCancellationPolicy(cancellationPolicy.Code, cancellationPolicy.ToSnapshotJson());
                }

                if (isWalkIn)
                {
                    reservation.MarkAsWalkIn();
                }

                if (isOverbooking)
                {
                    reservation.MarkAsOverbooking();
                }

                // Rattachement au bloc : c'est lui qui dit si la nuitee CONSOMME l'allotement ou
                // mange l'inventaire public.
                if (request.AllotmentId is { } attachedAllotmentId)
                {
                    reservation.AttachToAllotment(attachedAllotmentId);
                }
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
            {
                return ApplicationResult<ReservationResponse>.Validation(ex.Message);
            }

            var now = DateTimeOffset.UtcNow;
            reservation.MarkCreated(context.UserName, now);
            dbContext.Set<Reservation>().Add(reservation);

            if (room is not null)
            {
                dbContext.Set<StayRoomAssignment>().Add(new StayRoomAssignment(
                    reservation.Id,
                    room.Id,
                    room.Number,
                    room.RoomTypeCode,
                    now,
                    context.UserName,
                    "Affectation a la vente."));
            }

            AddJournalEntry(
                reservation,
                ReservationEventKind.Created,
                isWalkIn
                    ? $"Walk-in {reservation.Number} cree sur {roomTypeCode}."
                    : $"Dossier {reservation.Number} cree sur {roomTypeCode}.",
                context,
                now,
                businessDay.Date,
                newValue: reservation.Status.ToString());

            if (isOverbooking)
            {
                AddJournalEntry(
                    reservation,
                    ReservationEventKind.Note,
                    "Vente en SURRESERVATION : la capacite physique du type est depassee sur au moins une nuit.",
                    context,
                    now,
                    businessDay.Date);
            }

            // Acompte exige par le plan : demande automatiquement, pas encaisse. Le laisser a la
            // charge de l'operateur ferait passer a la trappe la garantie que le tarif exige.
            if (ratePlan is { DepositPercent: > 0m })
            {
                var amount = Math.Round(
                    reservation.TotalStayAmount * ratePlan.DepositPercent / 100m,
                    2,
                    MidpointRounding.AwayFromZero);

                if (amount > 0m)
                {
                    var deposit = new Deposit(
                        reservation.Id,
                        amount,
                        request.ArrivalDate.AddDays(-1),
                        $"Acompte de {ratePlan.DepositPercent:0.##} % exige par le plan {ratePlan.Code}.");

                    deposit.MarkCreated(context.UserName, now);
                    dbContext.Set<Deposit>().Add(deposit);
                }
            }

            await WriteAuditAsync(
                "lodging.reservation.created",
                ReservationsEntity,
                reservation.Id,
                context,
                new
                {
                    reservation.Number,
                    reservation.HotelUnitCode,
                    reservation.RoomTypeCode,
                    reservation.RoomId,
                    RoomNumber = room?.Number,
                    reservation.CustomerCode,
                    reservation.ArrivalDate,
                    reservation.DepartureDate,
                    reservation.Adults,
                    reservation.Children,
                    reservation.Infants,
                    reservation.NightlyRateSnapshot,
                    reservation.RatePlanCodeSnapshot,
                    reservation.TotalStayAmount,
                    reservation.IsOverbooking,
                    reservation.IsWalkIn
                },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<ReservationResponse>.Success(
                Map(reservation, room?.Number, customer.Name));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<ReservationResponse>.Conflict(RoomAlreadyReserved);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            // Deux creations simultanees ont pu tirer le meme numero de dossier : rien n'a ete
            // ecrit, l'appelant rejoue.
            return ApplicationResult<ReservationResponse>.Conflict(ConcurrentReservationMutationRefused);
        }
    }

    /// <summary>Verifie qu'un bloc a encore de la place, nuit par nuit, avant d'y prendre une chambre.</summary>
    private async Task<ApplicationResult<ReservationResponse>?> EnsureBlockHasRoomAsync(
        string hotelUnitCode,
        string roomTypeCode,
        Guid allotmentId,
        DateOnly arrivalDate,
        DateOnly departureDate,
        CancellationToken cancellationToken)
    {
        var allotment = await dbContext.Set<RoomAllotment>()
            .AsNoTracking()
            .FirstOrDefaultAsync(current => current.Id == allotmentId, cancellationToken);

        if (allotment is null)
        {
            return ApplicationResult<ReservationResponse>.NotFound("L'allotement est introuvable.");
        }

        if (!allotment.IsOpen)
        {
            return ApplicationResult<ReservationResponse>.Conflict(
                "Cet allotement est cloture : il ne tient plus de chambres.");
        }

        if (allotment.HotelUnitCode != hotelUnitCode || allotment.RoomTypeCode != roomTypeCode)
        {
            return ApplicationResult<ReservationResponse>.Validation(
                $"L'allotement {allotment.Reference} porte sur le type {allotment.RoomTypeCode} de l'unite "
                + $"{allotment.HotelUnitCode} : ce sejour n'en fait pas partie.");
        }

        if (arrivalDate < allotment.ArrivalDate || departureDate > allotment.DepartureDate)
        {
            return ApplicationResult<ReservationResponse>.Validation(
                $"L'allotement {allotment.Reference} couvre du {allotment.ArrivalDate:dd/MM/yyyy} au "
                + $"{allotment.DepartureDate:dd/MM/yyyy} : le sejour demande en sort.");
        }

        var taken = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => reservation.AllotmentId == allotmentId)
            .Where(BlocksPeriod(arrivalDate, departureDate))
            .Select(reservation => new { reservation.ArrivalDate, reservation.DepartureDate })
            .ToListAsync(cancellationToken);

        for (var night = arrivalDate; night < departureDate; night = night.AddDays(1))
        {
            var takenThatNight = taken.Count(entry =>
                entry.ArrivalDate <= night && night < entry.DepartureDate);

            if (takenThatNight >= allotment.RoomsHeld)
            {
                return ApplicationResult<ReservationResponse>.Conflict(
                    $"L'allotement {allotment.Reference} tient {allotment.RoomsHeld} chambre(s), toutes prises "
                    + $"la nuit du {night:dd/MM/yyyy}. Agrandissez le bloc ou vendez hors bloc.");
            }
        }

        return null;
    }

    /// <summary>
    /// Attribue le numero de dossier de l'unite pour l'annee en cours.
    ///
    /// L'allocation se fait DANS la transaction Serializable de la creation : deux ventes
    /// simultanees ne peuvent pas lire le meme maximum. L'index unique (unite, numero) est le
    /// dernier filet - il transforme une course perdue en conflit rejouable, jamais en doublon.
    /// </summary>
    private async Task<string> AllocateReservationNumberAsync(
        string hotelUnitCode,
        CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year % 100;
        var prefix = $"R{year:D2}";

        var last = await dbContext.Set<Reservation>()
            .Where(reservation => reservation.HotelUnitCode == hotelUnitCode
                && reservation.Number.StartsWith(prefix))
            .OrderByDescending(reservation => reservation.Number)
            .Select(reservation => reservation.Number)
            .FirstOrDefaultAsync(cancellationToken);

        var sequence = 1;

        if (last is not null
            && last.Length == prefix.Length + 6
            && int.TryParse(last[prefix.Length..], out var parsed))
        {
            sequence = parsed + 1;
        }

        return $"{prefix}{sequence:D6}";
    }

    private async Task<CancellationPolicy?> ResolveCancellationPolicyAsync(
        string hotelUnitCode,
        string? policyCode,
        CancellationToken cancellationToken)
    {
        if (policyCode is null)
        {
            return null;
        }

        return await dbContext.Set<CancellationPolicy>()
            .AsNoTracking()
            .Include(policy => policy.Rules)
            .FirstOrDefaultAsync(
                policy => policy.HotelUnitCode == hotelUnitCode
                    && policy.Code == policyCode
                    && policy.IsActive,
                cancellationToken);
    }

    public async Task<ApplicationResult<ReservationResponse>> UpdateReservationAsync(
        Guid id,
        UpdateReservationRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
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

        // La composition change ce que la chambre doit pouvoir coucher : on la revalide contre le
        // type VENDU. Sans ce controle, on pourrait passer de deux a quatre personnes dans une
        // chambre pour deux, et la reception le decouvrirait a l'arrivee.
        var roomType = await dbContext.Set<RoomType>()
            .AsNoTracking()
            .Include(type => type.Beds)
            .SingleOrDefaultAsync(
                type => type.HotelUnitCode == reservation.HotelUnitCode
                    && type.Code == reservation.RoomTypeCode,
                cancellationToken);

        if (roomType is not null
            && !roomType.CanHost(request.Adults, request.Children, request.Infants))
        {
            return ApplicationResult<ReservationResponse>.Validation(
                $"La composition demandee depasse ce que le type '{roomType.Code}' peut accueillir "
                + $"({roomType.MaxOccupancy} occupant(s), {roomType.MaxCots} berceau(x)).");
        }

        var now = DateTimeOffset.UtcNow;
        var businessDay = await ResolveBusinessDateAsync(reservation.HotelUnitCode, cancellationToken);
        var previousMix = $"{reservation.Adults}A/{reservation.Children}E/{reservation.Infants}B";

        try
        {
            reservation.ChangeGuestMix(request.Adults, request.Children, request.Infants);
            reservation.SetSchedule(request.EstimatedArrivalTime, request.EstimatedDepartureTime);
            reservation.SetCommercialContext(
                request.MarketSegmentCode,
                request.ChannelCode,
                request.SourceCode,
                request.CompanyCode,
                request.AgencyCode,
                reservation.ConventionCode);
            reservation.SetNotes(request.Notes, request.SpecialRequests);
            reservation.SetGuestName(request.GuestName);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return ApplicationResult<ReservationResponse>.Validation(ex.Message);
        }

        reservation.MarkUpdated(context.UserName, now);

        var currentMix = $"{reservation.Adults}A/{reservation.Children}E/{reservation.Infants}B";

        if (currentMix != previousMix)
        {
            AddJournalEntry(
                reservation,
                ReservationEventKind.GuestMixChanged,
                $"Composition : {previousMix} -> {currentMix}.",
                context,
                now,
                businessDay.Date,
                previousMix,
                currentMix);
        }

        await WriteAuditAsync(
            "lodging.reservation.updated",
            ReservationsEntity,
            reservation.Id,
            context,
            new
            {
                reservation.Number,
                reservation.HotelUnitCode,
                reservation.Adults,
                reservation.Children,
                reservation.Infants,
                reservation.EstimatedArrivalTime,
                reservation.EstimatedDepartureTime
            },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<ReservationResponse>.Success(
            Map(reservation, await LoadRoomNumberAsync(reservation.RoomId, cancellationToken)));
    }

    // ================================= Statuts commerciaux =================================

    public async Task<ApplicationResult<ReservationResponse>> ChangeReservationStatusAsync(
        Guid id,
        ChangeReservationStatusRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return await MutatePreArrivalReservationAsync(
            id,
            context,
            "lodging.reservation.status_changed",
            ReservationEventKind.StatusChanged,
            (reservation, now, businessDate) =>
            {
                var previous = reservation.Status;
                reservation.MoveToPreArrivalStatus(request.Status);

                return new MutationOutcome(
                    $"Statut : {previous} -> {reservation.Status}."
                    + (string.IsNullOrWhiteSpace(request.Reason) ? string.Empty : $" {request.Reason}"),
                    previous.ToString(),
                    reservation.Status.ToString());
            },
            cancellationToken);
    }

    public async Task<ApplicationResult<ReservationResponse>> SetGuaranteeAsync(
        Guid id,
        SetGuaranteeRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return await MutatePreArrivalReservationAsync(
            id,
            context,
            "lodging.reservation.guarantee_set",
            ReservationEventKind.GuaranteeChanged,
            (reservation, now, businessDate) =>
            {
                var previous = reservation.Guarantee;
                reservation.SetGuarantee(request.Guarantee, request.Reference);

                return new MutationOutcome(
                    $"Garantie : {previous} -> {reservation.Guarantee}.",
                    previous.ToString(),
                    reservation.Guarantee.ToString());
            },
            cancellationToken);
    }

    /// <summary>Ce qu'une mutation de dossier a change, pour l'audit et le journal de sejour.</summary>
    private sealed record MutationOutcome(string Summary, string? PreviousValue, string? NewValue);

    /// <summary>
    /// Squelette partage des gestes qui ne portent que sur un dossier d'AVANT ARRIVEE : transaction
    /// Serializable, claim conditionnel sur la famille de statuts, mutation du domaine, journal,
    /// audit. Le geste ne s'applique qu'a une ligne que la base vient de reconfirmer, de sorte
    /// qu'une arrivee concurrente ne peut pas etre silencieusement ecrasee.
    /// </summary>
    private async Task<ApplicationResult<ReservationResponse>> MutatePreArrivalReservationAsync(
        Guid id,
        OperationContext context,
        string auditAction,
        ReservationEventKind eventKind,
        Func<Reservation, DateTimeOffset, DateOnly, MutationOutcome> mutate,
        CancellationToken cancellationToken)
    {
        var preArrival = new[]
        {
            ReservationStatus.Inquiry,
            ReservationStatus.Option,
            ReservationStatus.Confirmed,
            ReservationStatus.Guaranteed
        };

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

            if (!reservation.Status.IsPreArrival())
            {
                return ApplicationResult<ReservationResponse>.Conflict(
                    "Cette operation n'est possible que sur un dossier d'avant-arrivee.");
            }

            var now = DateTimeOffset.UtcNow;

            if (!await TryClaimReservationStatusesAsync(reservation.Id, preArrival, now, cancellationToken))
            {
                return ApplicationResult<ReservationResponse>.Conflict(ConcurrentReservationMutationRefused);
            }

            var businessDay = await ResolveBusinessDateAsync(reservation.HotelUnitCode, cancellationToken);
            MutationOutcome outcome;

            try
            {
                outcome = mutate(reservation, now, businessDay.Date);
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
            {
                return ApplicationResult<ReservationResponse>.Validation(ex.Message);
            }

            reservation.MarkUpdated(context.UserName, now);

            AddJournalEntry(
                reservation,
                eventKind,
                outcome.Summary,
                context,
                now,
                businessDay.Date,
                outcome.PreviousValue,
                outcome.NewValue);

            await WriteAuditAsync(
                auditAction,
                ReservationsEntity,
                reservation.Id,
                context,
                new
                {
                    reservation.Number,
                    reservation.HotelUnitCode,
                    outcome.Summary,
                    outcome.PreviousValue,
                    outcome.NewValue
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

    /// <summary>Ajoute une ligne au journal metier du sejour.</summary>
    private void AddJournalEntry(
        Reservation reservation,
        ReservationEventKind kind,
        string summary,
        OperationContext context,
        DateTimeOffset occurredAt,
        DateOnly businessDate,
        string? previousValue = null,
        string? newValue = null)
    {
        dbContext.Set<ReservationEvent>().Add(new ReservationEvent(
            reservation.Id,
            kind,
            summary,
            occurredAt,
            context.UserName,
            previousValue,
            newValue,
            businessDate));
    }

    // ===================================== Projections =====================================

    private static ReservationResponse Map(Reservation reservation, string? roomNumber, string? customerName = null)
    {
        return new ReservationResponse(
            reservation.Id,
            reservation.HotelUnitCode,
            reservation.RoomId,
            roomNumber,
            reservation.CustomerCode,
            reservation.ArrivalDate,
            reservation.DepartureDate,
            reservation.Nights,
            reservation.GuestCount,
            reservation.Status,
            reservation.NightlyRateSnapshot,
            reservation.RatePlanCodeSnapshot,
            reservation.CancelReason,
            reservation.CheckedInAt,
            reservation.CheckedInBy,
            reservation.CheckedOutAt,
            reservation.CheckedOutBy,
            reservation.CancelledAt,
            reservation.CancelledBy,
            reservation.NoShowAt,
            reservation.NoShowBy,
            reservation.CreatedAt,
            reservation.CreatedBy,
            reservation.UpdatedAt,
            reservation.UpdatedBy,
            reservation.Number,
            reservation.RoomTypeCode,
            reservation.OriginalRoomTypeCode,
            reservation.Adults,
            reservation.Children,
            reservation.Infants,
            reservation.EstimatedArrivalTime,
            reservation.EstimatedDepartureTime,
            reservation.MarketSegmentCode,
            reservation.ChannelCode,
            reservation.SourceCode,
            reservation.CompanyCode,
            reservation.AgencyCode,
            reservation.ConventionCode,
            reservation.IsWalkIn,
            reservation.IsOverbooking,
            reservation.Notes,
            reservation.SpecialRequests,
            reservation.Guarantee,
            reservation.GuaranteeReference,
            reservation.CancellationPolicyCode,
            reservation.CancellationPolicySnapshotJson is { } snapshot
                ? CancellationPolicy.DescribeSnapshot(snapshot)
                : null,
            reservation.CancellationFeeAmount,
            reservation.TotalStayAmount,
            reservation.AllotmentId,
            reservation.GuestName,
            customerName);
    }

    private static ReservationEventResponse Map(ReservationEvent entry)
    {
        return new ReservationEventResponse(
            entry.Id,
            entry.Kind,
            entry.Summary,
            entry.OccurredAt,
            entry.BusinessDate,
            entry.Actor,
            entry.PreviousValue,
            entry.NewValue);
    }

    private static StayRoomAssignmentResponse Map(StayRoomAssignment assignment)
    {
        return new StayRoomAssignmentResponse(
            assignment.Id,
            assignment.RoomId,
            assignment.RoomNumber,
            assignment.RoomTypeCode,
            assignment.AssignedAt,
            assignment.AssignedBy,
            assignment.ReleasedAt,
            assignment.ReleasedBy,
            assignment.Reason,
            assignment.IsCurrent);
    }
}
