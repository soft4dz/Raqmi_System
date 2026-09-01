using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Lodging;

/// <summary>
/// Referentiel du parc : types de chambres et chambres physiques, avec leur couchage et leurs
/// attributs commerciaux.
/// </summary>
public sealed partial class LodgingService
{
    // ==================================== Types de chambres ====================================

    public async Task<IReadOnlyCollection<RoomTypeResponse>> ListRoomTypesAsync(
        string? hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<RoomType>().AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(roomType => roomType.IsActive);
        }

        var normalizedUnitCode = NormalizeNullableCode(hotelUnitCode);

        if (normalizedUnitCode is not null)
        {
            query = query.Where(roomType => roomType.HotelUnitCode == normalizedUnitCode);
        }

        var roomTypes = await query
            .Include(roomType => roomType.Beds)
            .OrderBy(roomType => roomType.HotelUnitCode)
            .ThenBy(roomType => roomType.DisplayOrder)
            .ThenBy(roomType => roomType.Code)
            .ToArrayAsync(cancellationToken);

        var activeRoomCounts = await GetActiveRoomCountsAsync(
            roomTypes.Select(roomType => roomType.HotelUnitCode).Distinct().ToArray(),
            cancellationToken);

        return roomTypes
            .Select(roomType => Map(
                roomType,
                activeRoomCounts.GetValueOrDefault(ActiveRoomCountKey(roomType.HotelUnitCode, roomType.Code))))
            .ToArray();
    }

    public async Task<ApplicationResult<RoomTypeResponse>> GetRoomTypeAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var roomType = await dbContext.Set<RoomType>()
            .AsNoTracking()
            .Include(current => current.Beds)
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (roomType is null)
        {
            return ApplicationResult<RoomTypeResponse>.NotFound("Le type de chambre est introuvable.");
        }

        return ApplicationResult<RoomTypeResponse>.Success(
            Map(roomType, await GetActiveRoomCountAsync(roomType, cancellationToken)));
    }

    public async Task<ApplicationResult<RoomTypeResponse>> CreateRoomTypeAsync(
        CreateRoomTypeRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var unitFailure = await RequireActiveHotelUnitAsync<RoomTypeResponse>(
            request.HotelUnitCode,
            cancellationToken);

        if (unitFailure.Failure is not null)
        {
            return unitFailure.Failure;
        }

        RoomType roomType;

        try
        {
            roomType = new RoomType(
                unitFailure.UnitCode,
                request.Code,
                request.Label,
                request.Capacity,
                request.Description);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<RoomTypeResponse>.Validation(ex.Message);
        }

        var profileFailure = ApplyRoomTypeProfile(roomType, request);

        if (profileFailure is not null)
        {
            return profileFailure;
        }

        var exists = await dbContext.Set<RoomType>()
            .AnyAsync(
                current => current.HotelUnitCode == roomType.HotelUnitCode && current.Code == roomType.Code,
                cancellationToken);

        if (exists)
        {
            return ApplicationResult<RoomTypeResponse>.Conflict(
                "Un type de chambre portant ce code existe deja dans cette unite.");
        }

        roomType.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<RoomType>().Add(roomType);

        try
        {
            await WriteAuditAsync(
                "lodging.room_type.created",
                RoomTypesEntity,
                roomType.Id,
                context,
                new { roomType.HotelUnitCode, roomType.Code, roomType.Label, roomType.Capacity, roomType.Rank },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // Le controle d'existence ci-dessus et cette insertion ne sont pas atomiques : une
            // creation concurrente sur le meme couple (unite, code) perd la course contre la cle
            // alternative.
            return ApplicationResult<RoomTypeResponse>.Conflict(
                "Un type de chambre portant ce code existe deja dans cette unite.");
        }

        return ApplicationResult<RoomTypeResponse>.Success(
            Map(roomType, await GetActiveRoomCountAsync(roomType, cancellationToken)));
    }

    public async Task<ApplicationResult<RoomTypeResponse>> UpdateRoomTypeAsync(
        Guid id,
        UpdateRoomTypeRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var roomType = await dbContext.Set<RoomType>()
            .Include(current => current.Beds)
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (roomType is null)
        {
            return ApplicationResult<RoomTypeResponse>.NotFound("Le type de chambre est introuvable.");
        }

        try
        {
            roomType.UpdateDetails(request.Label, request.Capacity, request.Description);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<RoomTypeResponse>.Validation(ex.Message);
        }

        var profileFailure = ApplyRoomTypeProfile(
            roomType,
            new CreateRoomTypeRequest(
                roomType.HotelUnitCode,
                roomType.Code,
                request.Label,
                request.Capacity,
                request.Description,
                request.Beds,
                request.MaxExtraBeds,
                request.MaxCots,
                request.MaxAdults,
                request.MaxChildren,
                request.MaxInfants,
                request.BaseRate,
                request.SurfaceSquareMeters,
                request.Rank,
                request.Amenities,
                request.DisplayOrder));

        if (profileFailure is not null)
        {
            return profileFailure;
        }

        roomType.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "lodging.room_type.updated",
            RoomTypesEntity,
            roomType.Id,
            context,
            new { roomType.HotelUnitCode, roomType.Code, roomType.Label, roomType.Capacity, roomType.Rank },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<RoomTypeResponse>.Success(
            Map(roomType, await GetActiveRoomCountAsync(roomType, cancellationToken)));
    }

    public async Task<ApplicationResult<RoomTypeResponse>> SetRoomTypeActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var roomType = await dbContext.Set<RoomType>()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (roomType is null)
        {
            return ApplicationResult<RoomTypeResponse>.NotFound("Le type de chambre est introuvable.");
        }

        if (isActive)
        {
            roomType.Activate();
        }
        else
        {
            roomType.Deactivate();
        }

        roomType.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            isActive ? "lodging.room_type.activated" : "lodging.room_type.deactivated",
            RoomTypesEntity,
            roomType.Id,
            context,
            new { roomType.HotelUnitCode, roomType.Code, roomType.IsActive },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<RoomTypeResponse>.Success(
            Map(roomType, await GetActiveRoomCountAsync(roomType, cancellationToken)));
    }

    // ======================================== Chambres ========================================

    public async Task<IReadOnlyCollection<RoomResponse>> ListRoomsAsync(
        string? hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<Room>().AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(room => room.IsActive);
        }

        var normalizedUnitCode = NormalizeNullableCode(hotelUnitCode);

        if (normalizedUnitCode is not null)
        {
            query = query.Where(room => room.HotelUnitCode == normalizedUnitCode);
        }

        var rooms = await query
            .Include(room => room.Beds)
            .OrderBy(room => room.HotelUnitCode)
            .ThenBy(room => room.DisplayOrder)
            .ThenBy(room => room.Number)
            .ToArrayAsync(cancellationToken);

        // Le couchage rendu est l'EFFECTIF : celui de la chambre quand elle en declare un, celui de
        // son type sinon. La resolution se fait ici, en une requete pour tout le lot, pour que
        // l'ecran n'ait jamais a recomposer l'heritage lui-meme.
        var types = await LoadRoomTypesForAsync(rooms, cancellationToken);

        return rooms.Select(room => Map(room, FindType(types, room))).ToArray();
    }

    public async Task<ApplicationResult<RoomResponse>> GetRoomAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var room = await dbContext.Set<Room>()
            .AsNoTracking()
            .Include(current => current.Beds)
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (room is null)
        {
            return ApplicationResult<RoomResponse>.NotFound("La chambre est introuvable.");
        }

        var singleType = await LoadRoomTypesForAsync([room], cancellationToken);

        return ApplicationResult<RoomResponse>.Success(Map(room, FindType(singleType, room)));
    }

    public async Task<ApplicationResult<RoomResponse>> CreateRoomAsync(
        CreateRoomRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var unitFailure = await RequireActiveHotelUnitAsync<RoomResponse>(
            request.HotelUnitCode,
            cancellationToken);

        if (unitFailure.Failure is not null)
        {
            return unitFailure.Failure;
        }

        Room room;

        try
        {
            room = new Room(
                unitFailure.UnitCode,
                request.Number,
                request.RoomTypeCode,
                request.Floor,
                request.Notes);

            room.SetLocation(request.Building, request.Wing, request.InternalCode);
            room.SetAttributes(
                request.View,
                request.Amenities,
                request.IsAccessible,
                request.IsSmoking,
                request.DisplayOrder);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<RoomResponse>.Validation(ex.Message);
        }

        var roomTypeFailure = await RequireActiveRoomTypeAsync<RoomResponse>(
            room.HotelUnitCode,
            room.RoomTypeCode,
            cancellationToken);

        if (roomTypeFailure is not null)
        {
            return roomTypeFailure;
        }

        var bedFailure = await ApplyRoomBedsAsync(
            room,
            request.Beds,
            request.MaxExtraBeds,
            request.MaxCots,
            cancellationToken);

        if (bedFailure is not null)
        {
            return bedFailure;
        }

        var exists = await dbContext.Set<Room>()
            .AnyAsync(
                current => current.HotelUnitCode == room.HotelUnitCode && current.Number == room.Number,
                cancellationToken);

        if (exists)
        {
            return ApplicationResult<RoomResponse>.Conflict(
                "Une chambre portant ce numero existe deja dans cette unite.");
        }

        room.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<Room>().Add(room);

        try
        {
            await WriteAuditAsync(
                "lodging.room.created",
                RoomsEntity,
                room.Id,
                context,
                new { room.HotelUnitCode, room.Number, room.RoomTypeCode, room.Building, room.Floor },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<RoomResponse>.Conflict(
                "Une chambre portant ce numero ou ce code interne existe deja dans cette unite.");
        }

        return ApplicationResult<RoomResponse>.Success(Map(room, await FindTypeForAsync(room, cancellationToken)));
    }

    public async Task<ApplicationResult<RoomResponse>> UpdateRoomAsync(
        Guid id,
        UpdateRoomRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var room = await dbContext.Set<Room>()
            .Include(current => current.Beds)
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (room is null)
        {
            return ApplicationResult<RoomResponse>.NotFound("La chambre est introuvable.");
        }

        string normalizedRoomTypeCode;

        try
        {
            normalizedRoomTypeCode = RoomType.NormalizeCode(request.RoomTypeCode);
        }
        catch (ArgumentException ex)
        {
            return ApplicationResult<RoomResponse>.Validation(ex.Message);
        }

        var roomTypeFailure = await RequireActiveRoomTypeAsync<RoomResponse>(
            room.HotelUnitCode,
            normalizedRoomTypeCode,
            cancellationToken);

        if (roomTypeFailure is not null)
        {
            return roomTypeFailure;
        }

        // Changer le type d'une chambre DEJA VENDUE reecrirait ce que des clients ont achete : un
        // dossier pris sur "Double standard" se retrouverait sur une suite, ou l'inverse, sans que
        // personne ne l'ait decide. Le geste reste possible, mais seulement quand aucun sejour ne
        // court ni n'est a venir sur cette chambre.
        if (normalizedRoomTypeCode != room.RoomTypeCode)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var hasFutureStays = await dbContext.Set<Reservation>()
                .AsNoTracking()
                .Where(reservation => reservation.RoomId == room.Id && reservation.DepartureDate > today)
                .Where(BlocksPeriod(today, DateOnly.MaxValue))
                .AnyAsync(cancellationToken);

            if (hasFutureStays)
            {
                return ApplicationResult<RoomResponse>.Conflict(
                    "Cette chambre porte des sejours en cours ou a venir : changer son type reecrirait ce "
                    + "que ces clients ont achete. Deplacez ou soldez ces dossiers d'abord.");
            }
        }

        room.AssignRoomType(normalizedRoomTypeCode);

        try
        {
            room.UpdateDetails(request.Floor, request.Notes);
            room.SetLocation(request.Building, request.Wing, request.InternalCode);
            room.SetAttributes(
                request.View,
                request.Amenities,
                request.IsAccessible,
                request.IsSmoking,
                request.DisplayOrder);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<RoomResponse>.Validation(ex.Message);
        }

        var bedFailure = await ApplyRoomBedsAsync(
            room,
            request.Beds,
            request.MaxExtraBeds,
            request.MaxCots,
            cancellationToken);

        if (bedFailure is not null)
        {
            return bedFailure;
        }

        room.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "lodging.room.updated",
            RoomsEntity,
            room.Id,
            context,
            new { room.HotelUnitCode, room.Number, room.RoomTypeCode, room.Building, room.Floor },
            cancellationToken);

        try
        {
            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<RoomResponse>.Conflict(
                "Une chambre portant ce numero ou ce code interne existe deja dans cette unite.");
        }

        return ApplicationResult<RoomResponse>.Success(Map(room, await FindTypeForAsync(room, cancellationToken)));
    }

    public async Task<ApplicationResult<RoomResponse>> SetRoomActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var room = await dbContext.Set<Room>()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (room is null)
        {
            return ApplicationResult<RoomResponse>.NotFound("La chambre est introuvable.");
        }

        if (isActive)
        {
            room.Activate();
        }
        else
        {
            // Desactiver une chambre la retire du parc pour toujours : ce n'est pas un blocage
            // temporaire. Les sejours a venir dessus seraient orphelins.
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var hasFutureStays = await dbContext.Set<Reservation>()
                .AsNoTracking()
                .Where(reservation => reservation.RoomId == room.Id && reservation.DepartureDate > today)
                .Where(BlocksPeriod(today, DateOnly.MaxValue))
                .AnyAsync(cancellationToken);

            if (hasFutureStays)
            {
                return ApplicationResult<RoomResponse>.Conflict(
                    "Cette chambre porte des sejours en cours ou a venir. Pour la retirer temporairement, "
                    + "utilisez un blocage hors service plutot que la desactivation.");
            }

            room.Deactivate();
        }

        room.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            isActive ? "lodging.room.activated" : "lodging.room.deactivated",
            RoomsEntity,
            room.Id,
            context,
            new { room.HotelUnitCode, room.Number, room.IsActive },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<RoomResponse>.Success(Map(room, await FindTypeForAsync(room, cancellationToken)));
    }

    // ============================ Couchage : application et controle ============================

    /// <summary>
    /// Applique le couchage standard d'un type, ses couchages d'appoint, sa composition commerciale
    /// et ses attributs de gamme. Le refus d'une composition dont le total ne correspond pas a la
    /// capacite vient du domaine : c'est la seule garantie que la recherche de disponibilite et le
    /// couchage affiche racontent la meme chose.
    /// </summary>
    private static ApplicationResult<RoomTypeResponse>? ApplyRoomTypeProfile(
        RoomType roomType,
        CreateRoomTypeRequest request)
    {
        try
        {
            roomType.SetExtraSleeping(request.MaxExtraBeds, request.MaxCots);

            if (request.Beds is not null)
            {
                roomType.ReplaceBeds(
                    request.Beds.Select(line => new RoomTypeBed(ParseBedType(line.BedType), line.Quantity)));
            }

            roomType.SetGuestMix(request.MaxAdults, request.MaxChildren, request.MaxInfants);
            roomType.SetCommercialProfile(
                request.BaseRate,
                request.SurfaceSquareMeters,
                request.Rank,
                request.DisplayOrder);
            roomType.SetAmenities(request.Amenities);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<RoomTypeResponse>.Validation(ex.Message);
        }

        return null;
    }

    /// <summary>
    /// Applique le couchage propre a une chambre et ses couchages d'appoint. Rend un echec
    /// applicatif plutot qu'une exception : une composition incoherente est une erreur de saisie,
    /// pas un incident technique.
    /// </summary>
    private async Task<ApplicationResult<RoomResponse>?> ApplyRoomBedsAsync(
        Room room,
        IReadOnlyCollection<BedCompositionLine>? beds,
        int? maxExtraBeds,
        int? maxCots,
        CancellationToken cancellationToken)
    {
        try
        {
            room.SetExtraSleeping(maxExtraBeds, maxCots);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return ApplicationResult<RoomResponse>.Validation(ex.Message);
        }

        if (beds is null)
        {
            return null;
        }

        var roomType = await FindTypeForAsync(room, cancellationToken);

        if (roomType is null)
        {
            return ApplicationResult<RoomResponse>.Validation(
                "Le type de chambre est introuvable : impossible de controler le couchage.");
        }

        try
        {
            room.ReplaceBeds(
                beds.Select(line => new RoomBed(ParseBedType(line.BedType), line.Quantity)),
                roomType.Capacity);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<RoomResponse>.Validation(ex.Message);
        }

        return null;
    }

    private async Task<RoomType?> FindTypeForAsync(Room room, CancellationToken cancellationToken)
    {
        return await dbContext.Set<RoomType>()
            .AsNoTracking()
            .Include(roomType => roomType.Beds)
            .FirstOrDefaultAsync(
                roomType => roomType.HotelUnitCode == room.HotelUnitCode && roomType.Code == room.RoomTypeCode,
                cancellationToken);
    }

    private static BedType ParseBedType(string value)
    {
        if (Enum.TryParse<BedType>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new ArgumentException($"Nature de couchage inconnue : {value}.", nameof(value));
    }

    /// <summary>Charge les types couvrant un lot de chambres, couchage compris.</summary>
    private async Task<IReadOnlyList<RoomType>> LoadRoomTypesForAsync(
        IReadOnlyCollection<Room> rooms,
        CancellationToken cancellationToken)
    {
        if (rooms.Count == 0)
        {
            return [];
        }

        var unitCodes = rooms.Select(room => room.HotelUnitCode).Distinct().ToList();

        return await dbContext.Set<RoomType>()
            .AsNoTracking()
            .Include(roomType => roomType.Beds)
            .Where(roomType => unitCodes.Contains(roomType.HotelUnitCode))
            .ToListAsync(cancellationToken);
    }

    private static RoomType? FindType(IReadOnlyList<RoomType> types, Room room)
    {
        return types.FirstOrDefault(type =>
            type.HotelUnitCode == room.HotelUnitCode && type.Code == room.RoomTypeCode);
    }

    // Nombre de chambres ACTIVES rattachees a chaque type de l'unite. Ce n'est pas une propriete de
    // l'entite mais une donnee d'ecran : le parametrage montre ce que la desactivation d'un type
    // bloquerait. Calcule a la projection, donc toujours exact - aucun compteur denormalise a
    // maintenir.
    private async Task<Dictionary<string, int>> GetActiveRoomCountsAsync(
        IReadOnlyCollection<string> hotelUnitCodes,
        CancellationToken cancellationToken)
    {
        if (hotelUnitCodes.Count == 0)
        {
            return [];
        }

        var counts = await dbContext.Set<Room>()
            .AsNoTracking()
            .Where(room => hotelUnitCodes.Contains(room.HotelUnitCode) && room.IsActive)
            .GroupBy(room => new { room.HotelUnitCode, room.RoomTypeCode })
            .Select(group => new { group.Key.HotelUnitCode, group.Key.RoomTypeCode, Count = group.Count() })
            .ToArrayAsync(cancellationToken);

        // Le code de type n'est unique QUE dans son unite : la cle combine les deux.
        return counts.ToDictionary(
            row => ActiveRoomCountKey(row.HotelUnitCode, row.RoomTypeCode),
            row => row.Count,
            StringComparer.Ordinal);
    }

    private async Task<int> GetActiveRoomCountAsync(RoomType roomType, CancellationToken cancellationToken)
    {
        return await dbContext.Set<Room>()
            .AsNoTracking()
            .CountAsync(
                room => room.HotelUnitCode == roomType.HotelUnitCode
                    && room.RoomTypeCode == roomType.Code
                    && room.IsActive,
                cancellationToken);
    }

    private static string ActiveRoomCountKey(string hotelUnitCode, string roomTypeCode)
    {
        return $"{hotelUnitCode}/{roomTypeCode}";
    }

    // ======================================= Projections =======================================

    private static RoomTypeResponse Map(RoomType roomType, int activeRoomCount)
    {
        return new RoomTypeResponse(
            roomType.Id,
            roomType.HotelUnitCode,
            roomType.Code,
            roomType.Label,
            roomType.Capacity,
            roomType.Description,
            roomType.IsActive,
            activeRoomCount,
            roomType.Beds
                .OrderBy(bed => bed.BedType)
                .Select(bed => new BedCompositionLine(bed.BedType.ToString(), bed.Quantity))
                .ToList(),
            roomType.DeclaredSleeps,
            roomType.MaxExtraBeds,
            roomType.MaxCots,
            roomType.MaxOccupancy,
            roomType.CreatedAt,
            roomType.CreatedBy,
            roomType.UpdatedAt,
            roomType.UpdatedBy,
            roomType.MaxAdults,
            roomType.MaxChildren,
            roomType.MaxInfants,
            roomType.BaseRate,
            roomType.SurfaceSquareMeters,
            roomType.Rank,
            roomType.GetAmenities(),
            roomType.DisplayOrder);
    }

    /// <summary>
    /// Projette une chambre avec son couchage EFFECTIF. <paramref name="roomType"/> peut etre nul
    /// quand le type est introuvable - la chambre reste alors affichable, sans composition heritee,
    /// plutot que de disparaitre de la liste pour un referentiel incomplet.
    /// </summary>
    private static RoomResponse Map(Room room, RoomType? roomType)
    {
        var beds = room.OverridesBeds
            ? room.Beds
                .OrderBy(bed => bed.BedType)
                .Select(bed => new BedCompositionLine(bed.BedType.ToString(), bed.Quantity))
                .ToList()
            : roomType?.Beds
                .OrderBy(bed => bed.BedType)
                .Select(bed => new BedCompositionLine(bed.BedType.ToString(), bed.Quantity))
                .ToList() ?? [];

        return new RoomResponse(
            room.Id,
            room.HotelUnitCode,
            room.Number,
            room.RoomTypeCode,
            room.Floor,
            room.Notes,
            room.IsActive,
            beds,
            room.OverridesBeds,
            room.MaxExtraBeds ?? roomType?.MaxExtraBeds ?? 0,
            room.MaxCots ?? roomType?.MaxCots ?? 0,
            room.CreatedAt,
            room.CreatedBy,
            room.UpdatedAt,
            room.UpdatedBy,
            room.Building,
            room.Wing,
            room.InternalCode,
            room.View,
            room.GetAmenities(),
            room.IsAccessible,
            room.IsSmoking,
            room.DisplayOrder,
            roomType?.Label,
            roomType?.Capacity ?? 0);
    }
}
