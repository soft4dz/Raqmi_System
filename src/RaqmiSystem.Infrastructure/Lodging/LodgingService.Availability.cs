using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Tariffs;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Lodging;
using System.Globalization;

namespace RaqmiSystem.Infrastructure.Lodging;

/// <summary>
/// La recherche de disponibilite et la tarification qui l'accompagne : le chemin le plus chaud du
/// PMS, et celui dont tous les autres dependent.
/// </summary>
public sealed partial class LodgingService
{
    public Task<ApplicationResult<AvailabilityResponse>> GetAvailabilityAsync(
        string hotelUnitCode,
        DateOnly from,
        DateOnly to,
        int guests,
        string? customerCode,
        CancellationToken cancellationToken)
    {
        // La signature historique (nombre de personnes indifferencie) reste le chemin simple du
        // comptoir. Elle se traduit en une recherche complete ou tout le monde est adulte : c'est
        // la lecture la plus prudente, puisqu'un adulte occupe un couchage plein.
        return SearchAvailabilityAsync(
            new AvailabilitySearchRequest(
                hotelUnitCode,
                from,
                to,
                Adults: guests,
                CustomerCode: customerCode),
            cancellationToken);
    }

    public async Task<ApplicationResult<AvailabilityResponse>> SearchAvailabilityAsync(
        AvailabilitySearchRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateSearch(request);

        if (validation is not null)
        {
            return validation;
        }

        var normalizedUnitCode = NormalizeCodeOrEmpty(request.HotelUnitCode);

        var unitFailure = await RequireHotelUnitAsync<AvailabilityResponse>(normalizedUnitCode, cancellationToken);

        if (unitFailure is not null)
        {
            return unitFailure;
        }

        // Meme discipline client que la creation vers laquelle la recherche mene : annoncer des
        // tarifs de convention a un client qui ne peut pas reserver afficherait des prix qu'aucune
        // reservation ne pourra honorer.
        var normalizedCustomerCode = NormalizeNullableCode(request.CustomerCode);

        if (normalizedCustomerCode is not null)
        {
            var customer = await dbContext.Set<Customer>()
                .AsNoTracking()
                .SingleOrDefaultAsync(current => current.Code == normalizedCustomerCode, cancellationToken);

            if (customer is null)
            {
                return ApplicationResult<AvailabilityResponse>.NotFound("Le client est introuvable.");
            }

            if (!customer.IsActive)
            {
                return ApplicationResult<AvailabilityResponse>.Validation(
                    "Aucune reservation ne peut etre prise pour un client inactif.");
            }
        }

        var nights = request.To.DayNumber - request.From.DayNumber;
        var policy = await GetPolicyEntityAsync(normalizedUnitCode, cancellationToken);
        var restrictions = await LoadRestrictionsAsync(normalizedUnitCode, request.From, request.To, cancellationToken);
        var bookingDate = await ResolveBusinessDateAsync(normalizedUnitCode, cancellationToken);

        // Les restrictions POSEES SUR L'HOTEL, tous types confondus. Elles sont evaluees a part et
        // rendues explicitement : un ecran vide sans explication ferait croire a une occupation
        // complete alors que la vente est simplement fermee.
        var unitDecision = RestrictionSet.Evaluate(
            restrictions.Where(restriction => restriction.RoomTypeCode is null),
            request.From,
            request.To,
            bookingDate.Date,
            roomTypeCode: null,
            request.RatePlanCode,
            request.ChannelCode);

        var normalizedTypeCode = NormalizeNullableCode(request.RoomTypeCode);

        var roomTypesQuery = dbContext.Set<RoomType>()
            .AsNoTracking()
            .Where(roomType => roomType.HotelUnitCode == normalizedUnitCode && roomType.IsActive);

        if (normalizedTypeCode is not null)
        {
            roomTypesQuery = roomTypesQuery.Where(roomType => roomType.Code == normalizedTypeCode);
        }

        var roomTypes = await roomTypesQuery
            .Include(roomType => roomType.Beds)
            .OrderBy(roomType => roomType.Rank)
            .ThenBy(roomType => roomType.DisplayOrder)
            .ThenBy(roomType => roomType.Code)
            .ToArrayAsync(cancellationToken);

        var yieldRules = await LoadYieldRulesAsync(normalizedUnitCode, request.From, request.To, cancellationToken);
        var unitOccupancy = await GetUnitOccupancyByNightAsync(
            normalizedUnitCode,
            request.From,
            request.To,
            policy,
            cancellationToken);

        var typeResponses = new List<RoomTypeAvailabilityResponse>();
        var listableTypeCodes = new HashSet<string>(StringComparer.Ordinal);
        var listableRoomCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var pricingByType = new Dictionary<string, StayPricing>(StringComparer.Ordinal);

        foreach (var roomType in roomTypes)
        {
            if (!roomType.CanHost(request.Adults, request.Children, request.Infants))
            {
                continue;
            }

            var availability = await BuildRoomTypeAvailabilityAsync(
                normalizedUnitCode,
                roomType.Code,
                request.From,
                request.To,
                policy,
                excludeReservationId: null,
                cancellationToken);

            var capacity = AvailabilityCalculator.CapacityForPublicSale(availability, request.AllowOverbooking);

            var typeDecision = RestrictionSet.Evaluate(
                restrictions,
                request.From,
                request.To,
                bookingDate.Date,
                roomType.Code,
                request.RatePlanCode,
                request.ChannelCode);

            var pricing = await ResolveStayRatesAsync(
                normalizedUnitCode,
                roomType.Code,
                request.From,
                request.To,
                normalizedCustomerCode,
                request.RatePlanCode,
                unitOccupancy,
                bookingDate.Date,
                yieldRules,
                cancellationToken);

            // UN TYPE SANS TARIF RESTE AFFICHE. Il a des chambres libres et la vente n'est pas
            // fermee : ce qui manque est le PARAMETRAGE TARIFAIRE, et le cacher deguiserait une
            // erreur de configuration en hotel complet. Il porte HasRate=false et le message du
            // resolveur ; la creation, elle, le refusera - un sejour sans prix n'est pas un sejour.
            var isListable = capacity.Rooms >= request.Rooms && typeDecision.IsAllowed;

            if (isListable)
            {
                listableTypeCodes.Add(roomType.Code);
                listableRoomCounts[roomType.Code] = capacity.Rooms;
            }

            pricingByType[roomType.Code] = pricing;

            typeResponses.Add(new RoomTypeAvailabilityResponse(
                roomType.Code,
                roomType.Label,
                roomType.Capacity,
                roomType.MaxOccupancy,
                roomType.Rank,
                availability.PublicAvailable,
                availability.CommercialAvailable,
                availability.SellableCapacity,
                availability.RequiresOverbooking,
                pricing.HasRate,
                DescribeRateIssue(pricing),
                pricing.RatePlanCode,
                pricing.HasRate ? pricing.Total : null,
                pricing.Nights,
                availability.Nights
                    .Select(night => MapInventory(night, restrictions, roomType.Code, request, bookingDate.Date))
                    .ToArray(),
                typeDecision.Violations.Select(violation => violation.Message).ToArray()));
        }

        var rooms = request.IncludePhysicalRooms
            ? await ListFreeRoomsAsync(
                normalizedUnitCode,
                request.From,
                request.To,
                listableTypeCodes,
                listableRoomCounts,
                pricingByType,
                cancellationToken)
            : [];

        return ApplicationResult<AvailabilityResponse>.Success(new AvailabilityResponse(
            normalizedUnitCode,
            request.From,
            request.To,
            nights,
            request.Adults + request.Children,
            rooms,
            typeResponses,
            unitDecision.Violations.Select(violation => violation.Message).ToArray(),
            unitDecision.Violations.Any(violation => violation.Kind == RestrictionViolationKind.Closed)));
    }

    private static ApplicationResult<AvailabilityResponse>? ValidateSearch(AvailabilitySearchRequest request)
    {
        if (request.To <= request.From)
        {
            return ApplicationResult<AvailabilityResponse>.Validation(
                "La date de fin doit etre posterieure a la date de debut (une recherche couvre au moins une nuit).");
        }

        if (request.To.DayNumber - request.From.DayNumber > MaxAvailabilityWindowNights)
        {
            return ApplicationResult<AvailabilityResponse>.Validation(
                $"La fenetre de recherche ne peut pas depasser {MaxAvailabilityWindowNights} nuits.");
        }

        if (request.Adults <= 0)
        {
            return ApplicationResult<AvailabilityResponse>.Validation(
                "Une recherche compte au moins un adulte.");
        }

        if (request.Children < 0 || request.Infants < 0)
        {
            return ApplicationResult<AvailabilityResponse>.Validation(
                "Le nombre d'enfants et de bebes ne peut pas etre negatif.");
        }

        if (request.Rooms <= 0)
        {
            return ApplicationResult<AvailabilityResponse>.Validation(
                "Le nombre de chambres demandees doit etre strictement positif.");
        }

        return null;
    }

    /// <summary>
    /// Les chambres PHYSIQUES libres sur la periode, pour l'affectation.
    ///
    /// LE NOMBRE PROPOSE EST TRONQUE AU DISPONIBLE COMMERCIAL DU TYPE, et c'est l'invariant le plus
    /// important de cette methode : la recherche ne doit JAMAIS proposer plus de chambres que la
    /// creation n'en accepterait. Sans cette troncature, un allotement qui tient trois doubles
    /// laisserait apparaitre trois chambres libres que toute tentative de vente publique refuserait
    /// - l'operateur buterait sur un mur invisible, ou pire, l'hotel survendrait si la creation
    /// etait plus laxiste.
    /// </summary>
    private async Task<IReadOnlyCollection<AvailableRoomResponse>> ListFreeRoomsAsync(
        string hotelUnitCode,
        DateOnly from,
        DateOnly to,
        IReadOnlySet<string> listableTypeCodes,
        IReadOnlyDictionary<string, int> listableRoomCounts,
        IReadOnlyDictionary<string, StayPricing> pricingByType,
        CancellationToken cancellationToken)
    {
        if (listableTypeCodes.Count == 0)
        {
            return [];
        }

        var candidates = await (
            from room in dbContext.Set<Room>().AsNoTracking()
            where room.HotelUnitCode == hotelUnitCode && room.IsActive
            join roomType in dbContext.Set<RoomType>().AsNoTracking()
                on new { Unit = room.HotelUnitCode, Code = room.RoomTypeCode }
                equals new { Unit = roomType.HotelUnitCode, Code = roomType.Code }
            orderby room.DisplayOrder, room.Number
            select new
            {
                room.Id,
                room.Number,
                RoomTypeCode = roomType.Code,
                RoomTypeLabel = roomType.Label,
                roomType.Capacity,
                room.Floor,
                room.Building,
                room.View,
                room.IsAccessible,
                room.IsSmoking
            })
            .ToArrayAsync(cancellationToken);

        // Chambres prises sur la periode, par LA MEME expression de chevauchement que la garde de
        // creation : ce que cette recherche appelle libre est exactement ce qu'une creation
        // accepterait, a la course pres que sa transaction Serializable tranche ensuite.
        var occupiedRoomIds = (await dbContext.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => reservation.HotelUnitCode == hotelUnitCode && reservation.RoomId != null)
            .Where(BlocksPeriod(from, to))
            .Select(reservation => reservation.RoomId!.Value)
            .Distinct()
            .ToArrayAsync(cancellationToken))
            .ToHashSet();

        // Chambres retirees par un blocage, hors service TECHNIQUE comme d'EXPLOITATION. Ici,
        // contrairement au calcul d'inventaire, le hors service d'exploitation retire TOUJOURS la
        // chambre de cette liste : on ne propose pas d'installer un client dans une chambre
        // reservee a un usage interne, meme quand la politique la laisse dans l'inventaire vendable.
        var blockedRoomIds = (await dbContext.Set<RoomBlock>()
            .AsNoTracking()
            .Where(block => block.HotelUnitCode == hotelUnitCode
                && (block.Status == RoomBlockStatus.Planned || block.Status == RoomBlockStatus.Active)
                && block.StartDate < to
                && block.EndDate > from)
            .Select(block => block.RoomId)
            .Distinct()
            .ToArrayAsync(cancellationToken))
            .ToHashSet();

        var conditions = await LoadRoomConditionsAsync(hotelUnitCode, cancellationToken);
        var rooms = new List<AvailableRoomResponse>();

        foreach (var group in candidates
            .Where(candidate => listableTypeCodes.Contains(candidate.RoomTypeCode))
            .Where(candidate => !occupiedRoomIds.Contains(candidate.Id))
            .Where(candidate => !blockedRoomIds.Contains(candidate.Id))
            .GroupBy(candidate => candidate.RoomTypeCode))
        {
            var sellable = listableRoomCounts.GetValueOrDefault(group.Key);
            var pricing = pricingByType.GetValueOrDefault(group.Key);

            foreach (var candidate in group.Take(sellable))
            {
                rooms.Add(new AvailableRoomResponse(
                    candidate.Id,
                    candidate.Number,
                    candidate.RoomTypeCode,
                    candidate.RoomTypeLabel,
                    candidate.Capacity,
                    pricing?.HasRate ?? false,
                    pricing is null ? null : DescribeRateIssue(pricing),
                    pricing?.RatePlanCode,
                    pricing?.ConventionCustomerCode,
                    pricing?.DiscountPercent,
                    pricing is { HasRate: true } ? pricing.Total : null,
                    pricing?.Nights ?? [],
                    candidate.Floor,
                    candidate.Building,
                    candidate.View,
                    candidate.IsAccessible,
                    candidate.IsSmoking,
                    conditions.TryGetValue(candidate.Id, out var status)
                        ? status.ToString()
                        : nameof(Domain.Housekeeping.RoomConditionStatus.Clean)));
            }
        }

        return rooms;
    }

    /// <summary>
    /// Le message d'un trou tarifaire, prefixe de la nuit fautive. Le prefixe est pose ICI, a
    /// l'affichage : le message brut du resolveur reste ce que la creation rend, pour que l'API ne
    /// renvoie pas un texte habille la ou l'appelant attend la raison.
    /// </summary>
    private static string? DescribeRateIssue(StayPricing pricing)
    {
        if (pricing.Issue is null)
        {
            return null;
        }

        return pricing.IssueNight is { } night
            ? $"Nuit du {night.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} : {pricing.Issue}"
            : pricing.Issue;
    }

    private static NightInventoryResponse MapInventory(
        NightInventory night,
        IReadOnlyList<RateRestriction> restrictions,
        string roomTypeCode,
        AvailabilitySearchRequest request,
        DateOnly bookingDate)
    {
        var isClosed = restrictions.Any(restriction =>
            restriction.IsClosed
            && restriction.Covers(night.Night)
            && restriction.AppliesTo(roomTypeCode, request.RatePlanCode, request.ChannelCode));

        return new NightInventoryResponse(
            night.Night,
            night.PhysicalRooms,
            night.BlockedRooms,
            night.SoldRooms,
            night.AllotmentHolds,
            night.OverbookingAllowed,
            night.OverbookingUsed,
            night.SellableCapacity,
            night.PhysicalAvailable,
            night.PublicAvailable,
            night.CommercialAvailable,
            night.OccupancyPercent,
            isClosed);
    }

    // ==================================== Tarification ====================================

    /// <summary>
    /// Le resultat de la tarification d'un sejour : soit tous les tarifs, soit la premiere nuit qui
    /// bloque et pourquoi.
    /// </summary>
    private sealed record StayPricing(
        bool HasRate,
        string? Issue,
        DateOnly? IssueNight,
        string? RatePlanCode,
        decimal Total,
        IReadOnlyCollection<AvailableNightRateResponse> Nights,
        IReadOnlyList<ReservationNightRate> FrozenRates,
        string? ConventionCustomerCode,
        decimal? DiscountPercent);

    /// <summary>
    /// Resout le tarif de CHAQUE nuit d'un sejour, convention client appliquee puis regle de yield
    /// eventuelle.
    ///
    /// UNE NUIT SANS PRIX ARRETE TOUT. La recherche laisse le type VISIBLE avec le message du
    /// resolveur - un trou de couverture tarifaire doit se voir - mais la vente est refusee : une
    /// reservation avec une nuit non chiffree n'est pas une reservation, c'est une facture qu'on ne
    /// saura pas etablir.
    /// </summary>
    private async Task<StayPricing> ResolveStayRatesAsync(
        string hotelUnitCode,
        string roomTypeCode,
        DateOnly from,
        DateOnly to,
        string? customerCode,
        string? requestedRatePlanCode,
        IReadOnlyDictionary<DateOnly, decimal> occupancyByNight,
        DateOnly bookingDate,
        IReadOnlyList<YieldRule> yieldRules,
        CancellationToken cancellationToken)
    {
        var nightResponses = new List<AvailableNightRateResponse>();
        var frozen = new List<ReservationNightRate>();
        ResolvedNightlyRate? first = null;

        for (var night = from; night < to; night = night.AddDays(1))
        {
            var resolution = await tariffResolutionService.ResolveAsync(
                hotelUnitCode,
                roomTypeCode,
                night,
                customerCode,
                cancellationToken);

            if (!resolution.Succeeded || resolution.Value is null)
            {
                // Le message du RESOLVEUR est rendu tel quel : c'est lui qui sait pourquoi la nuit
                // n'a pas de prix, et le reecrire ferait perdre l'information. La nuit fautive
                // voyage a cote, pour que la recherche puisse la nommer a l'ecran sans que la
                // creation ait a lire un message habille.
                return new StayPricing(
                    false,
                    resolution.Error ?? "Le tarif n'a pas pu etre resolu.",
                    night,
                    first?.RatePlanCode,
                    0m,
                    nightResponses,
                    frozen,
                    null,
                    null);
            }

            var rate = resolution.Value;

            // Un plan explicitement demande ne peut pas etre remplace en silence : si la resolution
            // rend un autre plan, c'est que la convention ou le plan par defaut a pris le dessus, et
            // l'operateur doit le savoir plutot que de decouvrir un autre prix sur la facture.
            if (requestedRatePlanCode is not null
                && !string.Equals(rate.RatePlanCode, requestedRatePlanCode, StringComparison.OrdinalIgnoreCase))
            {
                var issue =
                    $"Nuit du {night.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} : le plan "
                    + $"'{requestedRatePlanCode}' ne couvre pas cette nuit ; le tarif resolu vient du plan "
                    + $"'{rate.RatePlanCode}'.";

                return new StayPricing(false, issue, night, rate.RatePlanCode, 0m, nightResponses, frozen, null, null);
            }

            var occupancy = occupancyByNight.TryGetValue(night, out var value) ? value : 0m;
            var leadDays = night.DayNumber - bookingDate.DayNumber;

            rate = ApplyYield(rate, yieldRules, roomTypeCode, night, occupancy, leadDays);

            first ??= rate;
            nightResponses.Add(new AvailableNightRateResponse(night, rate.Amount, rate.RatePlanCode));
            frozen.Add(new ReservationNightRate(night, rate.Amount, rate.RatePlanCode));
        }

        return new StayPricing(
            true,
            null,
            null,
            first?.RatePlanCode,
            nightResponses.Sum(entry => entry.Amount),
            nightResponses,
            frozen,
            first?.ConventionCustomerCode,
            first?.DiscountPercent);
    }

    /// <summary>
    /// Applique AU PLUS UNE regle de yield : la premiere applicable dans l'ordre des priorites.
    ///
    /// Le cumul est deliberement exclu. Trois regles a +10 % qui se declenchent la meme nuit
    /// produiraient +33 %, ce que personne n'a decide et que personne ne verrait venir. La regle
    /// retenue laisse son code dans le tarif resolu, et de la dans la reservation : un prix modifie
    /// dit toujours pourquoi.
    /// </summary>
    private static ResolvedNightlyRate ApplyYield(
        ResolvedNightlyRate rate,
        IReadOnlyList<YieldRule> rules,
        string roomTypeCode,
        DateOnly night,
        decimal occupancyPercent,
        int leadDays)
    {
        var applicable = rules
            .Where(rule => rule.AppliesTo(roomTypeCode, rate.RatePlanCode))
            .Where(rule => rule.Triggers(night, occupancyPercent, leadDays))
            .OrderBy(rule => rule.Priority)
            .ThenByDescending(rule => Math.Abs(rule.AdjustmentPercent))
            .FirstOrDefault();

        if (applicable is null)
        {
            return rate;
        }

        return rate.WithYield(
            applicable.Apply(rate.Amount),
            applicable.Code,
            applicable.Label,
            applicable.AdjustmentPercent);
    }

    private async Task<IReadOnlyList<YieldRule>> LoadYieldRulesAsync(
        string hotelUnitCode,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<YieldRule>()
            .AsNoTracking()
            .Where(rule => rule.HotelUnitCode == hotelUnitCode
                && rule.IsActive
                && rule.FromDate < to
                && rule.ToDate >= from)
            .OrderBy(rule => rule.Priority)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Le taux d'occupation PREVU de l'unite, nuit par nuit : c'est lui que les regles de yield
    /// lisent. Une occupation par type serait plus fine mais moins juste - un revenue manager
    /// arbitre sur le remplissage de l'hotel, pas sur celui d'un type isole.
    /// </summary>
    private async Task<Dictionary<DateOnly, decimal>> GetUnitOccupancyByNightAsync(
        string hotelUnitCode,
        DateOnly from,
        DateOnly to,
        LodgingPolicy policy,
        CancellationToken cancellationToken)
    {
        var physicalRooms = await dbContext.Set<Room>()
            .AsNoTracking()
            .CountAsync(room => room.HotelUnitCode == hotelUnitCode && room.IsActive, cancellationToken);

        var blocked = await GetBlockedRoomCountsAsync(hotelUnitCode, null, from, to, policy, cancellationToken);
        var sold = await GetSoldRoomCountsAsync(hotelUnitCode, null, from, to, null, cancellationToken);

        var occupancy = new Dictionary<DateOnly, decimal>();

        for (var night = from; night < to; night = night.AddDays(1))
        {
            var capacity = Math.Max(0, physicalRooms - blocked.Total.GetValueOrDefault(night));
            var soldRooms = sold.GetValueOrDefault(night);

            occupancy[night] = capacity == 0
                ? 0m
                : Math.Round((decimal)soldRooms * 100m / capacity, 2, MidpointRounding.AwayFromZero);
        }

        return occupancy;
    }

    // ===================================== Occupation =====================================

    public async Task<ApplicationResult<OccupancyResponse>> GetOccupancyAsync(
        string hotelUnitCode,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        if (to < from)
        {
            return ApplicationResult<OccupancyResponse>.Validation(
                "La date de debut ne peut pas etre posterieure a la date de fin.");
        }

        if (to.DayNumber - from.DayNumber + 1 > MaxOccupancyWindowDays)
        {
            return ApplicationResult<OccupancyResponse>.Validation(
                $"La fenetre d'occupation ne peut pas depasser {MaxOccupancyWindowDays} jours.");
        }

        var normalizedUnitCode = NormalizeCodeOrEmpty(hotelUnitCode);

        var unitFailure = await RequireHotelUnitAsync<OccupancyResponse>(normalizedUnitCode, cancellationToken);

        if (unitFailure is not null)
        {
            return unitFailure;
        }

        var totalActiveRooms = await dbContext.Set<Room>()
            .AsNoTracking()
            .CountAsync(room => room.HotelUnitCode == normalizedUnitCode && room.IsActive, cancellationToken);

        // Une nuit est occupee quand un sejour qui tient l'inventaire la couvre : option,
        // confirmee, garantie, en cours ET terminee comptent toutes (la definition unique
        // ReservationStatuses.Blocks). Exclure les sejours termines viderait l'historique
        // retroactivement - un client parti a bien consomme ces nuits, et un mois passe ne doit
        // pas se lire comme vide.
        var reservations = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => reservation.HotelUnitCode == normalizedUnitCode
                && reservation.Status != ReservationStatus.Inquiry
                && reservation.Status != ReservationStatus.Cancelled
                && reservation.Status != ReservationStatus.NoShow
                && reservation.ArrivalDate <= to
                && reservation.DepartureDate > from)
            .Select(reservation => new { reservation.Id, reservation.RoomId, reservation.ArrivalDate, reservation.DepartureDate })
            .ToArrayAsync(cancellationToken);

        var days = new List<OccupancyDayResponse>(to.DayNumber - from.DayNumber + 1);

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var night = day;

            // Une chambre affectee ne compte qu'une fois ; un sejour SANS chambre affectee compte
            // tout de meme - il consomme bien une chambre de son type, meme si personne n'a encore
            // dit laquelle. L'ignorer ferait lire comme libres des chambres deja vendues.
            var covering = reservations
                .Where(reservation => reservation.ArrivalDate <= night && night < reservation.DepartureDate)
                .ToArray();

            var occupiedRooms = covering.Where(entry => entry.RoomId is not null)
                .Select(entry => entry.RoomId!.Value)
                .Distinct()
                .Count()
                + covering.Count(entry => entry.RoomId is null);

            var ratePercent = totalActiveRooms == 0
                ? 0m
                : Math.Round(occupiedRooms * 100m / totalActiveRooms, 2, MidpointRounding.AwayFromZero);

            days.Add(new OccupancyDayResponse(day, totalActiveRooms, occupiedRooms, ratePercent));
        }

        return ApplicationResult<OccupancyResponse>.Success(
            new OccupancyResponse(normalizedUnitCode, from, to, days));
    }
}
