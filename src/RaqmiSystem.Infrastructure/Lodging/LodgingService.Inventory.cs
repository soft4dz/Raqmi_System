using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Mice;

namespace RaqmiSystem.Infrastructure.Lodging;

/// <summary>
/// L'INVENTAIRE : ce qui existe, ce qui est retire, ce qui est tenu, ce qu'on s'autorise a vendre
/// au-dela. Tout le reste du PMS lit ce fichier et ne recalcule jamais par lui-meme.
/// </summary>
public sealed partial class LodgingService
{
    // ============================== Le calcul central d'inventaire ==============================

    /// <summary>
    /// Construit l'inventaire nuit par nuit d'UN type sur [from, to), a partir des cinq sources qui
    /// le composent : parc physique actif, blocages OOO/OOS, nuitees vendues, chambres tenues par
    /// des groupes, autorisation de surreservation.
    ///
    /// C'EST LE SEUL ENDROIT OU CES CINQ SOURCES SE RENCONTRENT. La recherche de disponibilite, la
    /// creation de reservation, l'affectation, le changement de chambre, la prolongation, le
    /// previsionnel et le planning appellent tous cette methode. Un second calcul, meme correct le
    /// jour ou il est ecrit, finirait par diverger du premier - et une disponibilite qui diverge se
    /// paie en survente.
    ///
    /// <paramref name="excludeReservationId"/> retire un dossier du comptage : indispensable quand
    /// on revalide un sejour DEJA pris (prolongation, changement de chambre), qui sinon se
    /// bloquerait lui-meme.
    /// </summary>
    private async Task<RoomTypeAvailability> BuildRoomTypeAvailabilityAsync(
        string hotelUnitCode,
        string roomTypeCode,
        DateOnly from,
        DateOnly to,
        LodgingPolicy policy,
        Guid? excludeReservationId,
        CancellationToken cancellationToken)
    {
        var physicalRooms = await dbContext.Set<Room>()
            .AsNoTracking()
            .CountAsync(
                room => room.HotelUnitCode == hotelUnitCode
                    && room.RoomTypeCode == roomTypeCode
                    && room.IsActive,
                cancellationToken);

        var blocked = await GetBlockedRoomCountsAsync(hotelUnitCode, roomTypeCode, from, to, policy, cancellationToken);
        var sold = await GetSoldRoomCountsAsync(hotelUnitCode, roomTypeCode, from, to, excludeReservationId, cancellationToken);
        var holds = await GetAllotmentHoldsAsync(hotelUnitCode, roomTypeCode, from, to, cancellationToken);
        var overbooking = await GetOverbookingAsync(hotelUnitCode, roomTypeCode, from, to, policy, cancellationToken);

        return AvailabilityCalculator.Build(
            roomTypeCode,
            from,
            to,
            physicalRooms,
            blocked.Total,
            sold,
            holds,
            overbooking);
    }

    /// <summary>
    /// Chambres retirees par un blocage, nuit par nuit, pour un type.
    ///
    /// Le hors service TECHNIQUE retire toujours ; le hors service d'EXPLOITATION ne retire que si
    /// la politique de l'unite le dit (<see cref="LodgingPolicy.OutOfServiceReducesInventory"/>).
    /// Les deux comptages sont rendus separement en plus du total, parce que le previsionnel doit
    /// les afficher distinctement : une direction ne lit pas de la meme facon dix chambres en panne
    /// et dix chambres pretees au personnel.
    ///
    /// Le comptage porte sur des chambres DISTINCTES : deux blocages qui se chevauchent sur la meme
    /// chambre ne la retirent qu'une fois.
    /// </summary>
    private async Task<BlockedRoomCounts> GetBlockedRoomCountsAsync(
        string hotelUnitCode,
        string? roomTypeCode,
        DateOnly from,
        DateOnly to,
        LodgingPolicy policy,
        CancellationToken cancellationToken)
    {
        // Syntaxe de methode et non syntaxe de requete : "from" et "to" sont des parametres, et
        // "from" est un mot-cle contextuel qu'une expression de requete tente de reinterpreter.
        var query = dbContext.Set<RoomBlock>()
            .AsNoTracking()
            .Where(block => block.HotelUnitCode == hotelUnitCode
                && (block.Status == RoomBlockStatus.Planned || block.Status == RoomBlockStatus.Active)
                && block.StartDate < to
                && block.EndDate > from)
            .Join(
                dbContext.Set<Room>().AsNoTracking().Where(room => room.IsActive),
                block => block.RoomId,
                room => room.Id,
                (block, room) => new
                {
                    block.RoomId,
                    block.Kind,
                    block.StartDate,
                    block.EndDate,
                    room.RoomTypeCode
                });

        if (roomTypeCode is not null)
        {
            query = query.Where(entry => entry.RoomTypeCode == roomTypeCode);
        }

        var blocks = await query.ToArrayAsync(cancellationToken);

        var outOfOrder = new Dictionary<DateOnly, int>();
        var outOfService = new Dictionary<DateOnly, int>();
        var total = new Dictionary<DateOnly, int>();

        for (var night = from; night < to; night = night.AddDays(1))
        {
            var covering = blocks
                .Where(entry => entry.StartDate <= night && night < entry.EndDate)
                .ToArray();

            var oooRooms = covering
                .Where(entry => entry.Kind == RoomBlockKind.OutOfOrder)
                .Select(entry => entry.RoomId)
                .Distinct()
                .ToHashSet();

            // Une chambre a la fois en panne et bloquee pour usage interne n'est retiree qu'une
            // fois : le hors service technique l'emporte, et la chambre ne figure pas deux fois.
            var oosRooms = covering
                .Where(entry => entry.Kind == RoomBlockKind.OutOfService)
                .Select(entry => entry.RoomId)
                .Distinct()
                .Where(roomId => !oooRooms.Contains(roomId))
                .ToHashSet();

            if (oooRooms.Count > 0)
            {
                outOfOrder[night] = oooRooms.Count;
            }

            if (oosRooms.Count > 0)
            {
                outOfService[night] = oosRooms.Count;
            }

            var deducted = oooRooms.Count + (policy.OutOfServiceReducesInventory ? oosRooms.Count : 0);

            if (deducted > 0)
            {
                total[night] = deducted;
            }
        }

        return new BlockedRoomCounts(total, outOfOrder, outOfService);
    }

    /// <summary>
    /// Nuitees VENDUES par type, nuit par nuit.
    ///
    /// Le type retenu est celui de la CHAMBRE affectee quand il y en a une, et le type vendu sinon.
    /// C'est la seule lecture qui reste vraie apres un surclassement : le client paie une double
    /// mais dort dans une suite, et c'est la suite qui n'est plus vendable ce soir-la.
    /// </summary>
    private async Task<Dictionary<DateOnly, int>> GetSoldRoomCountsAsync(
        string hotelUnitCode,
        string? roomTypeCode,
        DateOnly from,
        DateOnly to,
        Guid? excludeReservationId,
        CancellationToken cancellationToken)
    {
        var query =
            from reservation in dbContext.Set<Reservation>().AsNoTracking()
            join room in dbContext.Set<Room>().AsNoTracking()
                on reservation.RoomId equals room.Id into assigned
            from room in assigned.DefaultIfEmpty()
            where reservation.HotelUnitCode == hotelUnitCode
            select new
            {
                reservation.Id,
                reservation.Status,
                reservation.ArrivalDate,
                reservation.DepartureDate,
                EffectiveTypeCode = room != null ? room.RoomTypeCode : reservation.RoomTypeCode
            };

        query = query.Where(entry => entry.Status != ReservationStatus.Inquiry
            && entry.Status != ReservationStatus.Cancelled
            && entry.Status != ReservationStatus.NoShow
            && entry.ArrivalDate < to
            && entry.DepartureDate > from);

        if (roomTypeCode is not null)
        {
            query = query.Where(entry => entry.EffectiveTypeCode == roomTypeCode);
        }

        if (excludeReservationId is { } excluded)
        {
            query = query.Where(entry => entry.Id != excluded);
        }

        var stays = await query.ToArrayAsync(cancellationToken);

        var sold = new Dictionary<DateOnly, int>();

        for (var night = from; night < to; night = night.AddDays(1))
        {
            var count = stays.Count(entry => entry.ArrivalDate <= night && night < entry.DepartureDate);

            if (count > 0)
            {
                sold[night] = count;
            }
        }

        return sold;
    }

    /// <summary>
    /// Chambres encore TENUES par des allotements, nuit par nuit, pour un couple (unite, type),
    /// deduction faite de celles deja prises sur ces blocs.
    ///
    /// C'est le calcul PARTAGE par la recherche de disponibilite et par le garde de creation, et
    /// c'est volontaire : les deux chemins doivent appliquer exactement la meme regle. Si la
    /// recherche cachait des chambres que la creation accepte encore, l'hotel survendrait ; si elle
    /// en montrait que la creation refuse, l'operateur buterait sur un mur invisible.
    /// </summary>
    private async Task<Dictionary<DateOnly, int>> GetAllotmentHoldsAsync(
        string hotelUnitCode,
        string roomTypeCode,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var holds = new Dictionary<DateOnly, int>();

        var allotments = await dbContext.Set<RoomAllotment>()
            .AsNoTracking()
            .Where(allotment => allotment.HotelUnitCode == hotelUnitCode
                && allotment.RoomTypeCode == roomTypeCode)
            .Where(allotment => allotment.Status == RoomAllotmentStatus.Draft
                || allotment.Status == RoomAllotmentStatus.Confirmed)
            .Where(allotment => allotment.ArrivalDate < to && allotment.DepartureDate > from)
            .ToListAsync(cancellationToken);

        if (allotments.Count == 0)
        {
            return holds;
        }

        var allotmentIds = allotments.Select(allotment => allotment.Id).ToList();

        var picked = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => reservation.AllotmentId != null
                && allotmentIds.Contains(reservation.AllotmentId.Value))
            .Where(BlocksPeriod(from, to))
            .Select(reservation => new
            {
                AllotmentId = reservation.AllotmentId!.Value,
                reservation.ArrivalDate,
                reservation.DepartureDate
            })
            .ToListAsync(cancellationToken);

        // La date d'observation decide si un bloc a passe sa date de release. La disponibilite
        // d'une meme nuit peut donc changer d'un jour a l'autre sans qu'aucune reservation n'ait
        // bouge : c'est exactement l'objet d'un release, mais il faut le savoir.
        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);

        for (var night = from; night < to; night = night.AddDays(1))
        {
            var total = 0;

            foreach (var allotment in allotments)
            {
                if (!allotment.IsHoldingOn(night, asOf))
                {
                    continue;
                }

                var takenThatNight = picked.Count(entry =>
                    entry.AllotmentId == allotment.Id
                    && entry.ArrivalDate <= night
                    && night < entry.DepartureDate);

                // Solde calcule PAR BLOC : un bloc sur-consomme ne vient pas compenser le solde
                // d'un autre, ce qui reviendrait a rendre a la vente des chambres encore promises.
                total += Math.Max(0, allotment.RoomsHeld - takenThatNight);
            }

            if (total > 0)
            {
                holds[night] = total;
            }
        }

        return holds;
    }

    /// <summary>
    /// Solde de surreservation autorise, nuit par nuit. Rend un dictionnaire VIDE des que
    /// l'interrupteur general de l'unite est coupe : c'est le geste qui ferme la surreservation
    /// d'un coup en periode tendue sans effacer le parametrage.
    /// </summary>
    private async Task<Dictionary<DateOnly, int>> GetOverbookingAsync(
        string hotelUnitCode,
        string roomTypeCode,
        DateOnly from,
        DateOnly to,
        LodgingPolicy policy,
        CancellationToken cancellationToken)
    {
        var allowed = new Dictionary<DateOnly, int>();

        if (!policy.OverbookingEnabled)
        {
            return allowed;
        }

        var allowances = await dbContext.Set<OverbookingAllowance>()
            .AsNoTracking()
            .Where(allowance => allowance.HotelUnitCode == hotelUnitCode
                && allowance.RoomTypeCode == roomTypeCode
                && allowance.IsActive
                && allowance.FromDate < to
                && allowance.ToDate >= from)
            .ToArrayAsync(cancellationToken);

        if (allowances.Length == 0)
        {
            return allowed;
        }

        for (var night = from; night < to; night = night.AddDays(1))
        {
            // Plusieurs autorisations couvrant la meme nuit ne s'additionnent PAS : on retient la
            // plus large. Les additionner ferait glisser une survente de +2 et une de +3 vers +5,
            // ce que personne n'a decide.
            var extra = allowances
                .Where(allowance => allowance.Covers(night))
                .Select(allowance => allowance.ExtraRooms)
                .DefaultIfEmpty(0)
                .Max();

            if (extra > 0)
            {
                allowed[night] = extra;
            }
        }

        return allowed;
    }

    /// <summary>
    /// La politique d'exploitation de l'unite. Une unite qui n'en a jamais declare recoit la
    /// politique PAR DEFAUT, non persistee : la plus prudente possible - rien de vendu en plus,
    /// rien de facture en plus.
    /// </summary>
    private async Task<LodgingPolicy> GetPolicyEntityAsync(
        string hotelUnitCode,
        CancellationToken cancellationToken)
    {
        var policy = await dbContext.Set<LodgingPolicy>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.HotelUnitCode == hotelUnitCode, cancellationToken);

        return policy ?? LodgingPolicy.CreateDefault(hotelUnitCode);
    }

    /// <summary>Les restrictions actives d'une unite touchant [from, to].</summary>
    private async Task<IReadOnlyList<RateRestriction>> LoadRestrictionsAsync(
        string hotelUnitCode,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<RateRestriction>()
            .AsNoTracking()
            .Where(restriction => restriction.HotelUnitCode == hotelUnitCode
                && restriction.IsActive
                && restriction.FromDate <= to
                && restriction.ToDate >= from)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Les trois comptages de chambres bloquees d'une periode.</summary>
    private sealed record BlockedRoomCounts(
        Dictionary<DateOnly, int> Total,
        Dictionary<DateOnly, int> OutOfOrder,
        Dictionary<DateOnly, int> OutOfService);

    // =================================== Blocages OOO / OOS ===================================

    public async Task<ApplicationResult<IReadOnlyCollection<RoomBlockResponse>>> ListRoomBlocksAsync(
        string hotelUnitCode,
        DateOnly? from,
        DateOnly? to,
        RoomBlockKind? kind,
        bool includeClosed,
        CancellationToken cancellationToken)
    {
        var normalizedUnitCode = NormalizeCodeOrEmpty(hotelUnitCode);

        var unitFailure = await RequireHotelUnitAsync<IReadOnlyCollection<RoomBlockResponse>>(
            normalizedUnitCode,
            cancellationToken);

        if (unitFailure is not null)
        {
            return unitFailure;
        }

        var query = dbContext.Set<RoomBlock>()
            .AsNoTracking()
            .Where(block => block.HotelUnitCode == normalizedUnitCode);

        if (!includeClosed)
        {
            query = query.Where(block =>
                block.Status == RoomBlockStatus.Planned || block.Status == RoomBlockStatus.Active);
        }

        if (kind is { } wanted)
        {
            query = query.Where(block => block.Kind == wanted);
        }

        // Semantique du chevauchement, comme partout ailleurs : un blocage est liste des que sa
        // periode touche la fenetre, pas seulement quand il y commence.
        if (from is { } start)
        {
            query = query.Where(block => block.EndDate > start);
        }

        if (to is { } end)
        {
            query = query.Where(block => block.StartDate <= end);
        }

        var blocks = await query
            .OrderBy(block => block.StartDate)
            .ThenBy(block => block.RoomId)
            .ToArrayAsync(cancellationToken);

        var policy = await GetPolicyEntityAsync(normalizedUnitCode, cancellationToken);
        var rooms = await LoadRoomSummariesAsync(
            blocks.Select(block => block.RoomId).Distinct().ToArray(),
            cancellationToken);

        return ApplicationResult<IReadOnlyCollection<RoomBlockResponse>>.Success(
            blocks.Select(block => Map(block, rooms.GetValueOrDefault(block.RoomId), policy)).ToArray());
    }

    public async Task<ApplicationResult<RoomBlockResponse>> GetRoomBlockAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var block = await dbContext.Set<RoomBlock>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (block is null)
        {
            return ApplicationResult<RoomBlockResponse>.NotFound("Le blocage est introuvable.");
        }

        var policy = await GetPolicyEntityAsync(block.HotelUnitCode, cancellationToken);
        var rooms = await LoadRoomSummariesAsync([block.RoomId], cancellationToken);

        return ApplicationResult<RoomBlockResponse>.Success(
            Map(block, rooms.GetValueOrDefault(block.RoomId), policy));
    }

    public async Task<ApplicationResult<RoomBlockResponse>> CreateRoomBlockAsync(
        string hotelUnitCode,
        CreateRoomBlockRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var unitFailure = await RequireActiveHotelUnitAsync<RoomBlockResponse>(hotelUnitCode, cancellationToken);

        if (unitFailure.Failure is not null)
        {
            return unitFailure.Failure;
        }

        var room = await dbContext.Set<Room>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == request.RoomId, cancellationToken);

        if (room is null || room.HotelUnitCode != unitFailure.UnitCode)
        {
            return ApplicationResult<RoomBlockResponse>.NotFound("La chambre est introuvable dans cette unite.");
        }

        RoomBlock block;

        try
        {
            block = new RoomBlock(
                unitFailure.UnitCode,
                room.Id,
                request.Kind,
                request.StartDate,
                request.EndDate,
                request.Reason,
                request.Category,
                request.MaintenanceReference,
                request.Comment);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<RoomBlockResponse>.Validation(ex.Message);
        }

        // BLOQUER UNE CHAMBRE HABITEE EST REFUSE. Le blocage retire la chambre de l'inventaire :
        // pose sur une periode ou un client dort, il mettrait ce client dehors sans que personne
        // ne le voie, et le sejour continuerait a exister sur une chambre officiellement absente.
        var occupied = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => reservation.RoomId == room.Id)
            .Where(BlocksPeriod(block.StartDate, block.EndDate))
            .Select(reservation => new { reservation.Id, reservation.ArrivalDate, reservation.DepartureDate })
            .FirstOrDefaultAsync(cancellationToken);

        if (occupied is not null)
        {
            return ApplicationResult<RoomBlockResponse>.Conflict(
                $"La chambre {room.Number} porte un sejour du {occupied.ArrivalDate:dd/MM/yyyy} au "
                + $"{occupied.DepartureDate:dd/MM/yyyy} sur cette periode. Deplacez le client avant de "
                + "bloquer la chambre.");
        }

        // Doublon exact : ce n'est pas une erreur d'inventaire (la chambre serait retiree une seule
        // fois), mais deux lignes pour un meme fait rendent l'ecran illisible.
        var duplicate = await dbContext.Set<RoomBlock>()
            .AsNoTracking()
            .AnyAsync(
                current => current.RoomId == room.Id
                    && current.Kind == block.Kind
                    && current.StartDate == block.StartDate
                    && current.EndDate == block.EndDate
                    && (current.Status == RoomBlockStatus.Planned || current.Status == RoomBlockStatus.Active),
                cancellationToken);

        if (duplicate)
        {
            return ApplicationResult<RoomBlockResponse>.Conflict(
                "Un blocage identique existe deja sur cette chambre et cette periode.");
        }

        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        if (block.StartDate <= today)
        {
            block.Activate();
        }

        block.MarkCreated(context.UserName, now);
        dbContext.Set<RoomBlock>().Add(block);

        // Le housekeeping doit savoir qu'une chambre sort du service : c'est lui qui cesse d'y
        // planifier des taches, et lui qui verra la chambre revenir.
        if (block.Kind == RoomBlockKind.OutOfOrder && block.StartDate <= today)
        {
            await MarkRoomOutOfOrderAsync(block, room, context, now, cancellationToken);
        }

        await WriteAuditAsync(
            "lodging.room_block.created",
            RoomBlocksEntity,
            block.Id,
            context,
            new
            {
                block.HotelUnitCode,
                RoomNumber = room.Number,
                Kind = block.Kind.ToString(),
                Category = block.Category.ToString(),
                block.StartDate,
                block.EndDate,
                block.Reason,
                block.MaintenanceReference
            },
            cancellationToken);

        await SaveAsync(cancellationToken);

        var policy = await GetPolicyEntityAsync(block.HotelUnitCode, cancellationToken);

        return ApplicationResult<RoomBlockResponse>.Success(
            Map(block, new RoomSummary(room.Number, room.RoomTypeCode), policy));
    }

    public async Task<ApplicationResult<RoomBlockResponse>> UpdateRoomBlockAsync(
        Guid id,
        UpdateRoomBlockRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var block = await dbContext.Set<RoomBlock>()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (block is null)
        {
            return ApplicationResult<RoomBlockResponse>.NotFound("Le blocage est introuvable.");
        }

        var previous = $"{block.StartDate:yyyy-MM-dd} -> {block.EndDate:yyyy-MM-dd}";

        try
        {
            block.Reschedule(
                request.StartDate,
                request.EndDate,
                request.Reason,
                request.Category,
                request.MaintenanceReference,
                request.Comment);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return ApplicationResult<RoomBlockResponse>.Validation(ex.Message);
        }

        // La nouvelle periode peut avoir avale un sejour qui n'etait pas concerne : on rejoue le
        // meme controle qu'a la creation.
        var occupied = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => reservation.RoomId == block.RoomId)
            .Where(BlocksPeriod(block.StartDate, block.EndDate))
            .AnyAsync(cancellationToken);

        if (occupied)
        {
            return ApplicationResult<RoomBlockResponse>.Conflict(
                "La nouvelle periode recouvre un sejour deja pris sur cette chambre.");
        }

        block.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "lodging.room_block.updated",
            RoomBlocksEntity,
            block.Id,
            context,
            new
            {
                block.HotelUnitCode,
                block.RoomId,
                Previous = previous,
                Current = $"{block.StartDate:yyyy-MM-dd} -> {block.EndDate:yyyy-MM-dd}",
                block.Reason
            },
            cancellationToken);

        await SaveAsync(cancellationToken);

        var policy = await GetPolicyEntityAsync(block.HotelUnitCode, cancellationToken);
        var rooms = await LoadRoomSummariesAsync([block.RoomId], cancellationToken);

        return ApplicationResult<RoomBlockResponse>.Success(
            Map(block, rooms.GetValueOrDefault(block.RoomId), policy));
    }

    public async Task<ApplicationResult<RoomBlockResponse>> CloseRoomBlockAsync(
        Guid id,
        CloseRoomBlockRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var block = await dbContext.Set<RoomBlock>()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (block is null)
        {
            return ApplicationResult<RoomBlockResponse>.NotFound("Le blocage est introuvable.");
        }

        var now = DateTimeOffset.UtcNow;

        try
        {
            block.Close(request.ReturnDate, context.UserName, now);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ApplicationResult<RoomBlockResponse>.Validation(ex.Message);
        }

        block.MarkUpdated(context.UserName, now);

        // Une chambre qui revient de travaux n'est pas propre : elle repart en SALE, pas en PROPRE.
        // La declarer propre ferait vendre une chambre que personne n'a inspectee.
        await MarkRoomDirtyAsync(block.HotelUnitCode, block.RoomId, context, now, cancellationToken);

        await WriteAuditAsync(
            "lodging.room_block.closed",
            RoomBlocksEntity,
            block.Id,
            context,
            new { block.HotelUnitCode, block.RoomId, block.ActualReturnDate, block.EndDate },
            cancellationToken);

        await SaveAsync(cancellationToken);

        var policy = await GetPolicyEntityAsync(block.HotelUnitCode, cancellationToken);
        var rooms = await LoadRoomSummariesAsync([block.RoomId], cancellationToken);

        return ApplicationResult<RoomBlockResponse>.Success(
            Map(block, rooms.GetValueOrDefault(block.RoomId), policy));
    }

    public async Task<ApplicationResult<RoomBlockResponse>> CancelRoomBlockAsync(
        Guid id,
        CancelRoomBlockRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var block = await dbContext.Set<RoomBlock>()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (block is null)
        {
            return ApplicationResult<RoomBlockResponse>.NotFound("Le blocage est introuvable.");
        }

        var now = DateTimeOffset.UtcNow;

        try
        {
            block.CancelBlock(request.Reason, context.UserName, now);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ApplicationResult<RoomBlockResponse>.Validation(ex.Message);
        }

        block.MarkUpdated(context.UserName, now);

        await WriteAuditAsync(
            "lodging.room_block.cancelled",
            RoomBlocksEntity,
            block.Id,
            context,
            new { block.HotelUnitCode, block.RoomId, block.CancelReason },
            cancellationToken);

        await SaveAsync(cancellationToken);

        var policy = await GetPolicyEntityAsync(block.HotelUnitCode, cancellationToken);
        var rooms = await LoadRoomSummariesAsync([block.RoomId], cancellationToken);

        return ApplicationResult<RoomBlockResponse>.Success(
            Map(block, rooms.GetValueOrDefault(block.RoomId), policy));
    }

    // =================================== Politique d'unite ===================================

    public async Task<ApplicationResult<LodgingPolicyResponse>> GetPolicyAsync(
        string hotelUnitCode,
        CancellationToken cancellationToken)
    {
        var normalizedUnitCode = NormalizeCodeOrEmpty(hotelUnitCode);

        var unitFailure = await RequireHotelUnitAsync<LodgingPolicyResponse>(normalizedUnitCode, cancellationToken);

        if (unitFailure is not null)
        {
            return unitFailure;
        }

        var stored = await dbContext.Set<LodgingPolicy>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.HotelUnitCode == normalizedUnitCode, cancellationToken);

        var policy = stored ?? LodgingPolicy.CreateDefault(normalizedUnitCode);

        return ApplicationResult<LodgingPolicyResponse>.Success(Map(policy, isDefault: stored is null));
    }

    public async Task<ApplicationResult<LodgingPolicyResponse>> SavePolicyAsync(
        string hotelUnitCode,
        SaveLodgingPolicyRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var unitFailure = await RequireActiveHotelUnitAsync<LodgingPolicyResponse>(hotelUnitCode, cancellationToken);

        if (unitFailure.Failure is not null)
        {
            return unitFailure.Failure;
        }

        if (request.CheckOutTime >= request.CheckInTime)
        {
            // Ce n'est pas une faute : beaucoup d'hotels ont un depart a 12h et une arrivee a 14h,
            // donc depart < arrivee. L'inverse - depart apres l'arrivee - laisserait deux clients
            // dans la meme chambre a la meme heure, et c'est cela qu'on refuse.
            return ApplicationResult<LodgingPolicyResponse>.Validation(
                "L'heure de depart doit preceder l'heure d'arrivee : sinon deux clients se croiseraient "
                + "dans la meme chambre.");
        }

        var policy = await dbContext.Set<LodgingPolicy>()
            .SingleOrDefaultAsync(current => current.HotelUnitCode == unitFailure.UnitCode, cancellationToken);

        var isNew = policy is null;
        policy ??= new LodgingPolicy(unitFailure.UnitCode);

        try
        {
            policy.SetCounterHours(request.CheckInTime, request.CheckOutTime);
            policy.SetEarlyCheckIn(
                request.EarlyCheckInFromTime,
                request.EarlyCheckInIsFree,
                request.EarlyCheckInFlatCharge,
                request.EarlyCheckInPercentOfNight);
            policy.SetLateCheckOut(
                request.LateCheckOutUntilTime,
                request.LateCheckOutIsFree,
                request.LateCheckOutFlatCharge,
                request.LateCheckOutPercentOfNight);
            policy.SetInventoryRules(request.OutOfServiceReducesInventory, request.OverbookingEnabled);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<LodgingPolicyResponse>.Validation(ex.Message);
        }

        var now = DateTimeOffset.UtcNow;

        if (isNew)
        {
            policy.MarkCreated(context.UserName, now);
            dbContext.Set<LodgingPolicy>().Add(policy);
        }
        else
        {
            policy.MarkUpdated(context.UserName, now);
        }

        await WriteAuditAsync(
            "lodging.policy.saved",
            PoliciesEntity,
            policy.Id,
            context,
            new
            {
                policy.HotelUnitCode,
                policy.CheckInTime,
                policy.CheckOutTime,
                policy.OutOfServiceReducesInventory,
                policy.OverbookingEnabled
            },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<LodgingPolicyResponse>.Success(Map(policy, isDefault: false));
    }

    private static LodgingPolicyResponse Map(LodgingPolicy policy, bool isDefault)
    {
        return new LodgingPolicyResponse(
            policy.HotelUnitCode,
            isDefault,
            policy.CheckInTime,
            policy.CheckOutTime,
            policy.EarlyCheckInFromTime,
            policy.EarlyCheckInIsFree,
            policy.EarlyCheckInFlatCharge,
            policy.EarlyCheckInPercentOfNight,
            policy.LateCheckOutUntilTime,
            policy.LateCheckOutIsFree,
            policy.LateCheckOutFlatCharge,
            policy.LateCheckOutPercentOfNight,
            policy.OutOfServiceReducesInventory,
            policy.OverbookingEnabled);
    }

    private static RoomBlockResponse Map(RoomBlock block, RoomSummary? room, LodgingPolicy policy)
    {
        var reducesInventory = block.Kind == RoomBlockKind.OutOfOrder || policy.OutOfServiceReducesInventory;

        return new RoomBlockResponse(
            block.Id,
            block.HotelUnitCode,
            block.RoomId,
            room?.Number,
            room?.RoomTypeCode,
            block.Kind,
            block.Category,
            block.StartDate,
            block.EndDate,
            block.ActualReturnDate,
            block.Nights,
            block.Reason,
            block.MaintenanceReference,
            block.Comment,
            block.Status,
            reducesInventory,
            block.ClosedAt,
            block.ClosedBy,
            block.CancelReason,
            block.CreatedAt,
            block.CreatedBy,
            block.UpdatedAt,
            block.UpdatedBy);
    }

    private async Task<Dictionary<Guid, RoomSummary>> LoadRoomSummariesAsync(
        Guid[] roomIds,
        CancellationToken cancellationToken)
    {
        if (roomIds.Length == 0)
        {
            return [];
        }

        return await dbContext.Set<Room>()
            .AsNoTracking()
            .Where(room => roomIds.Contains(room.Id))
            .ToDictionaryAsync(
                room => room.Id,
                room => new RoomSummary(room.Number, room.RoomTypeCode),
                cancellationToken);
    }

    private sealed record RoomSummary(string Number, string RoomTypeCode);
}
