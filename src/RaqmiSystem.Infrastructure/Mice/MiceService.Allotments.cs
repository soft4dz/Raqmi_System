using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Mice;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Mice;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Mice;

/// <summary>
/// Volet GROUPES du module 10.6 : allotements et rooming lists.
///
/// CE SERVICE NE CONTROLE PAS LA DISPONIBILITE LUI-MEME, et c'est delibere. Le solde tenu par un
/// bloc est calcule par LodgingService, au meme endroit que la recherche de disponibilite et que le
/// garde de creation de reservation. Reimplementer ici un second calcul serait la faute la plus
/// probable de tout ce module : deux formules pour une meme question finiraient par diverger, et la
/// divergence se traduirait par une survente.
///
/// De la meme facon, une chambre du bloc est prise en appelant ILodgingService : la reservation
/// d'un groupe doit passer exactement par le meme chemin, les memes garanties de concurrence et le
/// meme figeage de tarif qu'une reservation individuelle.
/// </summary>
public sealed partial class MiceService
{
    private const string AllotmentNotFound = "L'allotement est introuvable.";

    public async Task<IReadOnlyCollection<RoomAllotmentResponse>> ListAllotmentsAsync(
        string? hotelUnitCode,
        DateOnly? from,
        DateOnly? to,
        bool includeClosed,
        CancellationToken cancellationToken)
    {
        var query = dbContext.RoomAllotments.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(hotelUnitCode))
        {
            var normalized = HotelUnit.NormalizeCode(hotelUnitCode);
            query = query.Where(allotment => allotment.HotelUnitCode == normalized);
        }

        if (from is { } fromDate)
        {
            query = query.Where(allotment => allotment.DepartureDate > fromDate);
        }

        if (to is { } toDate)
        {
            query = query.Where(allotment => allotment.ArrivalDate < toDate);
        }

        if (!includeClosed)
        {
            query = query.Where(allotment => allotment.Status == RoomAllotmentStatus.Draft
                || allotment.Status == RoomAllotmentStatus.Confirmed);
        }

        var allotments = await query
            .OrderBy(allotment => allotment.ArrivalDate)
            .ThenBy(allotment => allotment.Reference)
            .ToListAsync(cancellationToken);

        return await MapManyAsync(allotments, cancellationToken);
    }

    public async Task<ApplicationResult<RoomAllotmentResponse>> CreateAllotmentAsync(
        CreateRoomAllotmentRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var unit = await ResolveUnitAsync(request.HotelUnitCode, cancellationToken);

        if (unit is null)
        {
            return ApplicationResult<RoomAllotmentResponse>.NotFound("L'unite hoteliere est introuvable.");
        }

        var customerFailure = await ValidateCustomerAsync(request.CustomerCode, cancellationToken);

        if (customerFailure is not null)
        {
            return ApplicationResult<RoomAllotmentResponse>.Validation(customerFailure);
        }

        var roomType = await FindRoomTypeAsync(unit, request.RoomTypeCode, cancellationToken);

        if (roomType is null)
        {
            return ApplicationResult<RoomAllotmentResponse>.NotFound("Le type de chambre est introuvable.");
        }

        if (!roomType.IsActive)
        {
            return ApplicationResult<RoomAllotmentResponse>.Validation(
                $"Le type {roomType.Code} est desactive : aucun bloc ne peut y etre pose.");
        }

        RoomAllotment allotment;

        try
        {
            allotment = new RoomAllotment(
                unit,
                request.Reference,
                request.CustomerCode,
                roomType.Code,
                request.ArrivalDate,
                request.DepartureDate,
                request.RoomsHeld,
                request.ReleaseDate,
                request.Notes);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<RoomAllotmentResponse>.Validation(ex.Message);
        }

        var referenceTaken = await dbContext.RoomAllotments.AnyAsync(
            item => item.HotelUnitCode == allotment.HotelUnitCode && item.Reference == allotment.Reference,
            cancellationToken);

        if (referenceTaken)
        {
            return ApplicationResult<RoomAllotmentResponse>.Conflict(
                $"La reference {allotment.Reference} est deja utilisee dans cette unite.");
        }

        // On refuse un bloc plus gros que ce que l'hotel possede : tenir 30 chambres dans un type
        // qui n'en compte que 12 bloquerait tout l'inventaire sans que personne comprenne pourquoi.
        var activeRooms = await dbContext.Set<Room>()
            .CountAsync(
                room => room.HotelUnitCode == allotment.HotelUnitCode
                    && room.RoomTypeCode == allotment.RoomTypeCode
                    && room.IsActive,
                cancellationToken);

        if (allotment.RoomsHeld > activeRooms)
        {
            return ApplicationResult<RoomAllotmentResponse>.Validation(
                $"L'unite ne compte que {activeRooms} chambre(s) active(s) de type {allotment.RoomTypeCode} : "
                + $"impossible d'en tenir {allotment.RoomsHeld}.");
        }

        allotment.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.RoomAllotments.Add(allotment);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<RoomAllotmentResponse>.Success(await MapAsync(allotment, cancellationToken));
    }

    public async Task<ApplicationResult<RoomAllotmentResponse>> UpdateAllotmentAsync(
        Guid id,
        UpdateRoomAllotmentRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var allotment = await dbContext.RoomAllotments
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (allotment is null)
        {
            return ApplicationResult<RoomAllotmentResponse>.NotFound(AllotmentNotFound);
        }

        // Le bloc ne peut pas devenir plus petit que sa propre consommation : les chambres deja
        // prises dessus existent, et un bloc de 5 portant 8 reservations serait un mensonge.
        var peak = await GetPeakPickupAsync(allotment, cancellationToken);

        if (request.RoomsHeld < peak)
        {
            return ApplicationResult<RoomAllotmentResponse>.Conflict(
                $"{peak} chambre(s) sont deja prises sur ce bloc la nuit la plus chargee : il ne peut pas "
                + $"etre reduit a {request.RoomsHeld}.");
        }

        try
        {
            allotment.UpdateBlock(
                request.ArrivalDate,
                request.DepartureDate,
                request.RoomsHeld,
                request.ReleaseDate,
                request.Notes);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<RoomAllotmentResponse>.Validation(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ApplicationResult<RoomAllotmentResponse>.Conflict(ex.Message);
        }

        allotment.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<RoomAllotmentResponse>.Success(await MapAsync(allotment, cancellationToken));
    }

    public Task<ApplicationResult<RoomAllotmentResponse>> ConfirmAllotmentAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return TransitionAsync(
            id,
            (allotment, userName, nowUtc) => allotment.Confirm(userName, nowUtc),
            context,
            cancellationToken);
    }

    public Task<ApplicationResult<RoomAllotmentResponse>> ReleaseAllotmentAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return TransitionAsync(
            id,
            (allotment, userName, nowUtc) => allotment.Release(userName, nowUtc),
            context,
            cancellationToken);
    }

    public async Task<ApplicationResult<RoomAllotmentResponse>> CancelAllotmentAsync(
        Guid id,
        CancelRoomAllotmentRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var allotment = await dbContext.RoomAllotments
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (allotment is null)
        {
            return ApplicationResult<RoomAllotmentResponse>.NotFound(AllotmentNotFound);
        }

        // Annuler un bloc portant des reservations les laisserait pointer vers un bloc inexistant,
        // et surtout les sortirait du calcul de solde : elles deviendraient invisibles au lieu de
        // devenir des ventes publiques. On exige que l'operateur les traite d'abord.
        var attached = await dbContext.Set<Reservation>()
            .CountAsync(reservation => reservation.AllotmentId == id, cancellationToken);

        if (attached > 0)
        {
            return ApplicationResult<RoomAllotmentResponse>.Conflict(
                $"{attached} reservation(s) sont rattachees a ce bloc. Annulez-les ou detachez-les avant "
                + "d'annuler l'allotement.");
        }

        try
        {
            allotment.Cancel(request.Reason, context.UserName, DateTimeOffset.UtcNow);
        }
        catch (ArgumentException ex)
        {
            return ApplicationResult<RoomAllotmentResponse>.Validation(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ApplicationResult<RoomAllotmentResponse>.Conflict(ex.Message);
        }

        allotment.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<RoomAllotmentResponse>.Success(await MapAsync(allotment, cancellationToken));
    }

    // ================================ Rooming lists ================================

    public async Task<ApplicationResult<RoomingListResponse>> GetRoomingListAsync(
        Guid allotmentId,
        CancellationToken cancellationToken)
    {
        var allotment = await dbContext.RoomAllotments
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == allotmentId, cancellationToken);

        if (allotment is null)
        {
            return ApplicationResult<RoomingListResponse>.NotFound(AllotmentNotFound);
        }

        var entries = await LoadRoomingEntriesAsync(allotmentId, cancellationToken);

        return ApplicationResult<RoomingListResponse>.Success(new RoomingListResponse(
            await MapAsync(allotment, cancellationToken),
            entries,
            []));
    }

    public async Task<ApplicationResult<RoomingListResponse>> SubmitRoomingListAsync(
        Guid allotmentId,
        IReadOnlyCollection<RoomingListEntryRequest> entries,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var allotment = await dbContext.RoomAllotments
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == allotmentId, cancellationToken);

        if (allotment is null)
        {
            return ApplicationResult<RoomingListResponse>.NotFound(AllotmentNotFound);
        }

        if (!allotment.IsOpen)
        {
            return ApplicationResult<RoomingListResponse>.Conflict(
                "Cet allotement est cloture : plus aucune chambre ne peut y etre attribuee.");
        }

        var rejected = new List<string>();

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.GuestName))
            {
                rejected.Add("Une ligne sans nom d'occupant a ete ignoree.");
                continue;
            }

            var arrival = entry.ArrivalDate ?? allotment.ArrivalDate;
            var departure = entry.DepartureDate ?? allotment.DepartureDate;

            var room = await FindFreeRoomOnBlockAsync(allotment, arrival, departure, cancellationToken);

            if (room is null)
            {
                rejected.Add($"{entry.GuestName} : aucune chambre libre de type {allotment.RoomTypeCode} "
                    + $"du {arrival:yyyy-MM-dd} au {departure:yyyy-MM-dd}.");
                continue;
            }

            // La reservation passe par ILodgingService : memes garanties de concurrence, meme
            // figeage de tarif et meme controle de solde qu'une vente individuelle. Le controle de
            // capacite du bloc est fait la-bas, dans la transaction Serializable.
            var created = await lodgingService.CreateReservationAsync(
                new CreateReservationRequest(
                    allotment.HotelUnitCode,
                    room.Id,
                    allotment.CustomerCode,
                    arrival,
                    departure,
                    entry.GuestCount <= 0 ? 1 : entry.GuestCount,
                    allotmentId,
                    entry.GuestName),
                context,
                cancellationToken);

            if (!created.Succeeded)
            {
                rejected.Add($"{entry.GuestName} : {created.Error}");
            }
        }

        var finalEntries = await LoadRoomingEntriesAsync(allotmentId, cancellationToken);

        var reloaded = await dbContext.RoomAllotments
            .AsNoTracking()
            .FirstAsync(item => item.Id == allotmentId, cancellationToken);

        return ApplicationResult<RoomingListResponse>.Success(new RoomingListResponse(
            await MapAsync(reloaded, cancellationToken),
            finalEntries,
            rejected));
    }

    // ================================== Interne ==================================

    private async Task<ApplicationResult<RoomAllotmentResponse>> TransitionAsync(
        Guid id,
        Action<RoomAllotment, string, DateTimeOffset> transition,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var allotment = await dbContext.RoomAllotments
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (allotment is null)
        {
            return ApplicationResult<RoomAllotmentResponse>.NotFound(AllotmentNotFound);
        }

        var nowUtc = DateTimeOffset.UtcNow;

        try
        {
            transition(allotment, context.UserName, nowUtc);
        }
        catch (InvalidOperationException ex)
        {
            return ApplicationResult<RoomAllotmentResponse>.Conflict(ex.Message);
        }

        allotment.MarkUpdated(context.UserName, nowUtc);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<RoomAllotmentResponse>.Success(await MapAsync(allotment, cancellationToken));
    }

    /// <summary>
    /// Premiere chambre du type libre sur toute la periode demandee. La recherche exclut les
    /// chambres deja occupees, y compris par les lignes de rooming list posees juste avant dans le
    /// meme envoi : elles sont deja en base a ce stade.
    /// </summary>
    private async Task<Room?> FindFreeRoomOnBlockAsync(
        RoomAllotment allotment,
        DateOnly arrival,
        DateOnly departure,
        CancellationToken cancellationToken)
    {
        var candidates = await dbContext.Set<Room>()
            .AsNoTracking()
            .Where(room => room.HotelUnitCode == allotment.HotelUnitCode
                && room.RoomTypeCode == allotment.RoomTypeCode
                && room.IsActive)
            .OrderBy(room => room.Number)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return null;
        }

        var candidateIds = candidates.Select(room => room.Id).ToList();

        var occupied = (await dbContext.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => candidateIds.Contains(reservation.RoomId))
            .Where(reservation => reservation.Status != ReservationStatus.Cancelled
                && reservation.Status != ReservationStatus.NoShow
                && reservation.ArrivalDate < departure
                && reservation.DepartureDate > arrival)
            .Select(reservation => reservation.RoomId)
            .Distinct()
            .ToListAsync(cancellationToken))
            .ToHashSet();

        return candidates.FirstOrDefault(room => !occupied.Contains(room.Id));
    }

    private async Task<IReadOnlyCollection<RoomingListEntryResponse>> LoadRoomingEntriesAsync(
        Guid allotmentId,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from reservation in dbContext.Set<Reservation>().AsNoTracking()
            join room in dbContext.Set<Room>().AsNoTracking() on reservation.RoomId equals room.Id
            where reservation.AllotmentId == allotmentId
            select new
            {
                reservation.Id,
                room.Number,
                reservation.GuestName,
                reservation.GuestCount,
                reservation.ArrivalDate,
                reservation.DepartureDate,
                reservation.Status
            })
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(row => row.Number, StringComparer.Ordinal)
            .Select(row => new RoomingListEntryResponse(
                row.Id,
                row.Number,
                row.GuestName,
                row.GuestCount,
                row.ArrivalDate,
                row.DepartureDate,
                row.Status.ToString()))
            .ToList();
    }

    /// <summary>
    /// Chambres du bloc prises la nuit la PLUS CHARGEE. C'est le chiffre qui compte pour savoir si
    /// le bloc peut etre reduit : une moyenne autoriserait a descendre sous ce qui est reellement
    /// occupe un soir donne.
    /// </summary>
    private async Task<int> GetPeakPickupAsync(RoomAllotment allotment, CancellationToken cancellationToken)
    {
        var taken = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => reservation.AllotmentId == allotment.Id)
            .Where(reservation => reservation.Status != ReservationStatus.Cancelled
                && reservation.Status != ReservationStatus.NoShow)
            .Select(reservation => new { reservation.ArrivalDate, reservation.DepartureDate })
            .ToListAsync(cancellationToken);

        if (taken.Count == 0)
        {
            return 0;
        }

        var peak = 0;

        for (var night = allotment.ArrivalDate; night < allotment.DepartureDate; night = night.AddDays(1))
        {
            var count = taken.Count(entry => entry.ArrivalDate <= night && night < entry.DepartureDate);

            if (count > peak)
            {
                peak = count;
            }
        }

        return peak;
    }

    private async Task<RoomType?> FindRoomTypeAsync(
        string hotelUnitCode,
        string roomTypeCode,
        CancellationToken cancellationToken)
    {
        string normalized;

        try
        {
            normalized = RoomType.NormalizeCode(roomTypeCode);
        }
        catch (ArgumentException)
        {
            return null;
        }

        return await dbContext.Set<RoomType>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                type => type.HotelUnitCode == hotelUnitCode && type.Code == normalized,
                cancellationToken);
    }

    private async Task<RoomAllotmentResponse> MapAsync(RoomAllotment allotment, CancellationToken cancellationToken)
    {
        var mapped = await MapManyAsync([allotment], cancellationToken);

        return mapped[0];
    }

    private async Task<IReadOnlyList<RoomAllotmentResponse>> MapManyAsync(
        IReadOnlyCollection<RoomAllotment> allotments,
        CancellationToken cancellationToken)
    {
        if (allotments.Count == 0)
        {
            return [];
        }

        var customerCodes = allotments.Select(item => item.CustomerCode).Distinct().ToList();

        var customers = await dbContext.Customers
            .AsNoTracking()
            .Where(customer => customerCodes.Contains(customer.Code))
            .Select(customer => new { customer.Code, customer.Name })
            .ToListAsync(cancellationToken);

        var customerNames = customers.ToDictionary(item => item.Code, item => item.Name, StringComparer.Ordinal);

        var unitCodes = allotments.Select(item => item.HotelUnitCode).Distinct().ToList();

        var roomTypes = await dbContext.Set<RoomType>()
            .AsNoTracking()
            .Where(type => unitCodes.Contains(type.HotelUnitCode))
            .Select(type => new { type.HotelUnitCode, type.Code, type.Label })
            .ToListAsync(cancellationToken);

        var typeLabels = roomTypes.ToDictionary(
            type => $"{type.HotelUnitCode}/{type.Code}",
            type => type.Label,
            StringComparer.Ordinal);

        var asOf = DateOnly.FromDateTime(DateTime.Today);
        var result = new List<RoomAllotmentResponse>(allotments.Count);

        foreach (var allotment in allotments)
        {
            var peak = await GetPeakPickupAsync(allotment, cancellationToken);

            // "Tient-il encore ?" se juge sur la premiere nuit du bloc : c'est la que la date de
            // release mord en premier, et c'est la reponse que l'exploitant attend.
            var isHolding = allotment.IsHoldingOn(allotment.ArrivalDate, asOf);

            result.Add(new RoomAllotmentResponse(
                allotment.Id,
                allotment.HotelUnitCode,
                allotment.Reference,
                allotment.CustomerCode,
                customerNames.TryGetValue(allotment.CustomerCode, out var name) ? name : allotment.CustomerCode,
                allotment.RoomTypeCode,
                typeLabels.TryGetValue($"{allotment.HotelUnitCode}/{allotment.RoomTypeCode}", out var label)
                    ? label
                    : allotment.RoomTypeCode,
                allotment.ArrivalDate,
                allotment.DepartureDate,
                allotment.Nights,
                allotment.RoomsHeld,
                allotment.ReleaseDate,
                allotment.Status.ToString(),
                isHolding,
                peak,
                Math.Max(0, allotment.RoomsHeld - peak),
                allotment.Notes,
                allotment.CancelReason));
        }

        return result;
    }
}
