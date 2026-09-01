using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Domain.Closing;
using RaqmiSystem.Domain.Crm;
using RaqmiSystem.Domain.Housekeeping;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Infrastructure.Lodging;

/// <summary>
/// L'exploitation quotidienne : date metier, tableaux d'arrivees et de departs, clients presents,
/// planning graphique.
/// </summary>
public sealed partial class LodgingService
{
    /// <summary>
    /// La date metier hoteliere de l'unite : le LENDEMAIN de la derniere journee cloturee, ou la
    /// date calendaire quand rien n'a jamais ete cloture.
    ///
    /// Elle est lue par presque tous les gestes du module - c'est elle qui rattache une
    /// consommation, une nuitee ou un no-show a la bonne journee d'exploitation. Une journee
    /// REOUVERTE ne compte pas comme cloturee : elle est de nouveau en cours, et la date metier
    /// recule avec elle, ce qui est exactement le comportement attendu quand on rouvre une journee
    /// pour la corriger.
    /// </summary>
    private async Task<BusinessDay> ResolveBusinessDateAsync(
        string hotelUnitCode,
        CancellationToken cancellationToken)
    {
        var closings = await dbContext.Set<DailyClosing>()
            .AsNoTracking()
            .Where(closing => closing.HotelUnitCode == hotelUnitCode && closing.Status == ClosingStatus.Closed)
            .Select(closing => closing.BusinessDate)
            .ToArrayAsync(cancellationToken);

        DateOnly? lastClosed = closings.Length == 0 ? null : closings.Max();

        return BusinessDay.Resolve(lastClosed, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task<ApplicationResult<BusinessDateResponse>> GetBusinessDateAsync(
        string hotelUnitCode,
        CancellationToken cancellationToken)
    {
        var normalizedUnitCode = NormalizeCodeOrEmpty(hotelUnitCode);

        var unitFailure = await RequireHotelUnitAsync<BusinessDateResponse>(normalizedUnitCode, cancellationToken);

        if (unitFailure is not null)
        {
            return unitFailure;
        }

        var businessDay = await ResolveBusinessDateAsync(normalizedUnitCode, cancellationToken);

        return ApplicationResult<BusinessDateResponse>.Success(new BusinessDateResponse(
            normalizedUnitCode,
            businessDay.Date,
            businessDay.CalendarDate,
            businessDay.LastClosedDate,
            businessDay.HasClosing,
            businessDay.IsLate,
            businessDay.PendingDays));
    }

    // ==================================== Tableau arrivees ====================================

    public async Task<ApplicationResult<ArrivalBoardResponse>> GetArrivalsAsync(
        string hotelUnitCode,
        DateOnly? date,
        CancellationToken cancellationToken)
    {
        var normalizedUnitCode = NormalizeCodeOrEmpty(hotelUnitCode);

        var unitFailure = await RequireHotelUnitAsync<ArrivalBoardResponse>(normalizedUnitCode, cancellationToken);

        if (unitFailure is not null)
        {
            return unitFailure;
        }

        var businessDay = await ResolveBusinessDateAsync(normalizedUnitCode, cancellationToken);
        var day = date ?? businessDay.Date;
        var policy = await GetPolicyEntityAsync(normalizedUnitCode, cancellationToken);

        // Les arrivees ATTENDUES : celles du jour et celles en retard. Les secondes sont les
        // candidats no-show, et la reception doit les traiter d'abord - ce sont les seules qui
        // immobilisent une chambre pour personne.
        var reservations = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => reservation.HotelUnitCode == normalizedUnitCode
                && reservation.ArrivalDate <= day
                && (reservation.Status == ReservationStatus.Option
                    || reservation.Status == ReservationStatus.Confirmed
                    || reservation.Status == ReservationStatus.Guaranteed))
            .OrderBy(reservation => reservation.ArrivalDate)
            .ThenBy(reservation => reservation.EstimatedArrivalTime)
            .ThenBy(reservation => reservation.Number)
            .ToArrayAsync(cancellationToken);

        var rows = await BuildArrivalRowsAsync(normalizedUnitCode, reservations, policy, cancellationToken);

        return ApplicationResult<ArrivalBoardResponse>.Success(new ArrivalBoardResponse(
            normalizedUnitCode,
            day,
            rows,
            rows.Sum(row => row.Adults + row.Children),
            rows.Count(row => row.RoomId is not null),
            rows.Count(row => row.RoomId is not null && !row.RoomIsReady),
            rows.Count(row => row.RoomId is null)));
    }

    private async Task<IReadOnlyList<ArrivalRowResponse>> BuildArrivalRowsAsync(
        string hotelUnitCode,
        IReadOnlyCollection<Reservation> reservations,
        LodgingPolicy policy,
        CancellationToken cancellationToken)
    {
        if (reservations.Count == 0)
        {
            return [];
        }

        var ids = reservations.Select(reservation => reservation.Id).ToArray();

        var roomNumbers = await LoadRoomNumbersAsync(
            reservations.Where(reservation => reservation.RoomId is not null)
                .Select(reservation => reservation.RoomId!.Value)
                .Distinct()
                .ToArray(),
            cancellationToken);

        var customerNames = await LoadCustomerNamesAsync(
            reservations.Select(reservation => reservation.CustomerCode).ToArray(),
            cancellationToken);

        var conditions = await LoadRoomConditionsAsync(hotelUnitCode, cancellationToken);

        var deposits = await dbContext.Set<Deposit>()
            .AsNoTracking()
            .Where(deposit => ids.Contains(deposit.ReservationId)
                && (deposit.Status == DepositStatus.Paid || deposit.Status == DepositStatus.Applied))
            .GroupBy(deposit => deposit.ReservationId)
            .Select(group => new { ReservationId = group.Key, Amount = group.Sum(deposit => deposit.Amount) })
            .ToDictionaryAsync(entry => entry.ReservationId, entry => entry.Amount, cancellationToken);

        // Le statut VIP vient du CRM : il ne se devine pas depuis l'hebergement, et l'afficher au
        // comptoir est precisement ce qui evite de faire attendre un client fidele.
        var customerCodes = reservations.Select(reservation => reservation.CustomerCode).Distinct().ToArray();

        var vipCodes = (await dbContext.Set<GuestProfile>()
            .AsNoTracking()
            .Where(profile => customerCodes.Contains(profile.CustomerCode) && profile.IsVip)
            .Select(profile => profile.CustomerCode)
            .ToArrayAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        return reservations.Select(reservation =>
        {
            var roomNumber = reservation.RoomId is { } roomId ? roomNumbers.GetValueOrDefault(roomId) : null;

            var housekeeping = reservation.RoomId is { } id && conditions.TryGetValue(id, out var status)
                ? status
                : RoomConditionStatus.Clean;

            // "Prete" veut dire propre ou inspectee. Une chambre sale ou hors service n'est pas
            // remettable, meme si le plan la montre libre.
            var isReady = reservation.RoomId is not null
                && housekeeping is RoomConditionStatus.Clean or RoomConditionStatus.Inspected;

            return new ArrivalRowResponse(
                reservation.Id,
                reservation.Number,
                reservation.CustomerCode,
                customerNames.GetValueOrDefault(reservation.CustomerCode),
                reservation.GuestName,
                reservation.RoomId,
                roomNumber,
                reservation.RoomTypeCode,
                reservation.RoomId is null ? null : housekeeping.ToString(),
                isReady,
                reservation.ArrivalDate,
                reservation.DepartureDate,
                reservation.Nights,
                reservation.Adults,
                reservation.Children,
                reservation.Infants,
                reservation.EstimatedArrivalTime,
                policy.IsEarlyCheckIn(reservation.EstimatedArrivalTime),
                reservation.Guarantee,
                reservation.TotalStayAmount,
                deposits.GetValueOrDefault(reservation.Id),
                reservation.TotalStayAmount - deposits.GetValueOrDefault(reservation.Id),
                vipCodes.Contains(reservation.CustomerCode),
                reservation.AllotmentId is not null,
                reservation.SpecialRequests,
                reservation.Status);
        }).ToArray();
    }

    // ===================================== Tableau departs =====================================

    public async Task<ApplicationResult<DepartureBoardResponse>> GetDeparturesAsync(
        string hotelUnitCode,
        DateOnly? date,
        CancellationToken cancellationToken)
    {
        var normalizedUnitCode = NormalizeCodeOrEmpty(hotelUnitCode);

        var unitFailure = await RequireHotelUnitAsync<DepartureBoardResponse>(normalizedUnitCode, cancellationToken);

        if (unitFailure is not null)
        {
            return unitFailure;
        }

        var businessDay = await ResolveBusinessDateAsync(normalizedUnitCode, cancellationToken);
        var day = date ?? businessDay.Date;
        var policy = await GetPolicyEntityAsync(normalizedUnitCode, cancellationToken);

        // Departs du jour : ceux encore en cours et ceux deja enregistres, pour que l'ecran montre
        // ce qui reste a faire ET ce qui est fait.
        var reservations = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => reservation.HotelUnitCode == normalizedUnitCode
                && ((reservation.Status == ReservationStatus.CheckedIn && reservation.DepartureDate <= day)
                    || (reservation.Status == ReservationStatus.CheckedOut && reservation.DepartureDate == day)))
            .OrderBy(reservation => reservation.DepartureDate)
            .ThenBy(reservation => reservation.EstimatedDepartureTime)
            .ToArrayAsync(cancellationToken);

        var ids = reservations.Select(reservation => reservation.Id).ToArray();

        var balances = ids.Length == 0
            ? new Dictionary<Guid, decimal>()
            : (await dbContext.Set<Folio>()
                .AsNoTracking()
                .Include(folio => folio.Charges)
                .Where(folio => ids.Contains(folio.ReservationId))
                .ToArrayAsync(cancellationToken))
                .GroupBy(folio => folio.ReservationId)
                .ToDictionary(group => group.Key, group => group.Sum(folio => folio.Balance));

        var roomNumbers = await LoadRoomNumbersAsync(
            reservations.Where(reservation => reservation.RoomId is not null)
                .Select(reservation => reservation.RoomId!.Value)
                .Distinct()
                .ToArray(),
            cancellationToken);

        var customerNames = await LoadCustomerNamesAsync(
            reservations.Select(reservation => reservation.CustomerCode).ToArray(),
            cancellationToken);

        var rows = reservations.Select(reservation =>
        {
            var balance = balances.GetValueOrDefault(reservation.Id);

            return new DepartureRowResponse(
                reservation.Id,
                reservation.Number,
                reservation.RoomId,
                reservation.RoomId is { } roomId ? roomNumbers.GetValueOrDefault(roomId) : null,
                reservation.CustomerCode,
                customerNames.GetValueOrDefault(reservation.CustomerCode),
                reservation.ArrivalDate,
                reservation.DepartureDate,
                reservation.EstimatedDepartureTime,
                policy.IsLateCheckOut(reservation.EstimatedDepartureTime),
                balance,
                balance == 0m,
                reservation.Status == ReservationStatus.CheckedOut,
                reservation.CheckedOutAt);
        }).ToArray();

        return ApplicationResult<DepartureBoardResponse>.Success(new DepartureBoardResponse(
            normalizedUnitCode,
            day,
            rows,
            rows.Count(row => !row.CheckedOut),
            rows.Where(row => !row.CheckedOut).Sum(row => row.Balance)));
    }

    // ==================================== Clients presents ====================================

    public async Task<ApplicationResult<IReadOnlyCollection<InHouseGuestResponse>>> GetInHouseAsync(
        string hotelUnitCode,
        CancellationToken cancellationToken)
    {
        var normalizedUnitCode = NormalizeCodeOrEmpty(hotelUnitCode);

        var unitFailure = await RequireHotelUnitAsync<IReadOnlyCollection<InHouseGuestResponse>>(
            normalizedUnitCode,
            cancellationToken);

        if (unitFailure is not null)
        {
            return unitFailure;
        }

        var reservations = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => reservation.HotelUnitCode == normalizedUnitCode
                && reservation.Status == ReservationStatus.CheckedIn)
            .OrderBy(reservation => reservation.DepartureDate)
            .ToArrayAsync(cancellationToken);

        var ids = reservations.Select(reservation => reservation.Id).ToArray();

        var balances = ids.Length == 0
            ? new Dictionary<Guid, decimal>()
            : (await dbContext.Set<Folio>()
                .AsNoTracking()
                .Include(folio => folio.Charges)
                .Where(folio => ids.Contains(folio.ReservationId))
                .ToArrayAsync(cancellationToken))
                .GroupBy(folio => folio.ReservationId)
                .ToDictionary(group => group.Key, group => group.Sum(folio => folio.Balance));

        var roomNumbers = await LoadRoomNumbersAsync(
            reservations.Where(reservation => reservation.RoomId is not null)
                .Select(reservation => reservation.RoomId!.Value)
                .Distinct()
                .ToArray(),
            cancellationToken);

        var customerCodes = reservations.Select(reservation => reservation.CustomerCode).Distinct().ToArray();
        var customerNames = await LoadCustomerNamesAsync(customerCodes, cancellationToken);

        var profiles = await dbContext.Set<GuestProfile>()
            .AsNoTracking()
            .Where(profile => customerCodes.Contains(profile.CustomerCode))
            .ToDictionaryAsync(profile => profile.CustomerCode, cancellationToken);

        return ApplicationResult<IReadOnlyCollection<InHouseGuestResponse>>.Success(
            reservations.Select(reservation =>
            {
                profiles.TryGetValue(reservation.CustomerCode, out var profile);

                return new InHouseGuestResponse(
                    reservation.Id,
                    reservation.Number,
                    reservation.RoomId,
                    reservation.RoomId is { } roomId ? roomNumbers.GetValueOrDefault(roomId) : null,
                    reservation.RoomTypeCode,
                    reservation.CustomerCode,
                    customerNames.GetValueOrDefault(reservation.CustomerCode),
                    reservation.GuestName,
                    reservation.ArrivalDate,
                    reservation.DepartureDate,
                    reservation.Nights,
                    reservation.Adults,
                    reservation.Children,
                    reservation.Infants,
                    balances.GetValueOrDefault(reservation.Id),
                    reservation.SpecialRequests,
                    reservation.Notes,
                    profile?.IsVip ?? false,
                    profile?.SegmentCode);
            }).ToArray());
    }

    // ================================= Ecran comptoir (legacy) =================================

    public async Task<ApplicationResult<FrontDeskResponse>> GetFrontDeskAsync(
        string hotelUnitCode,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var normalizedUnitCode = NormalizeCodeOrEmpty(hotelUnitCode);

        var unitFailure = await RequireHotelUnitAsync<FrontDeskResponse>(normalizedUnitCode, cancellationToken);

        if (unitFailure is not null)
        {
            return unitFailure;
        }

        var expectedArrivals = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => reservation.HotelUnitCode == normalizedUnitCode
                && reservation.ArrivalDate <= date
                && (reservation.Status == ReservationStatus.Option
                    || reservation.Status == ReservationStatus.Confirmed
                    || reservation.Status == ReservationStatus.Guaranteed))
            .OrderBy(reservation => reservation.ArrivalDate)
            .ThenBy(reservation => reservation.CustomerCode)
            .ToArrayAsync(cancellationToken);

        var inHouseReservations = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => reservation.HotelUnitCode == normalizedUnitCode
                && reservation.Status == ReservationStatus.CheckedIn)
            .OrderBy(reservation => reservation.DepartureDate)
            .ThenBy(reservation => reservation.CustomerCode)
            .ToArrayAsync(cancellationToken);

        var arrivals = expectedArrivals.Where(reservation => reservation.ArrivalDate == date).ToArray();
        var overdueArrivals = expectedArrivals.Where(reservation => reservation.ArrivalDate < date).ToArray();
        var departures = inHouseReservations.Where(reservation => reservation.DepartureDate == date).ToArray();
        var overdueDepartures = inHouseReservations.Where(reservation => reservation.DepartureDate < date).ToArray();
        var inHouseCount = inHouseReservations.Count(reservation => reservation.CoversNight(date));

        var departureIds = departures.Concat(overdueDepartures)
            .Select(reservation => reservation.Id)
            .ToArray();

        var folioBalances = departureIds.Length == 0
            ? new Dictionary<Guid, decimal>()
            : (await dbContext.Set<Folio>()
                .AsNoTracking()
                .Include(folio => folio.Charges)
                .Where(folio => departureIds.Contains(folio.ReservationId))
                .ToArrayAsync(cancellationToken))
                .GroupBy(folio => folio.ReservationId)
                .ToDictionary(group => group.Key, group => group.Sum(folio => folio.Balance));

        var allListed = arrivals.Concat(overdueArrivals).Concat(departures).Concat(overdueDepartures).ToArray();

        var roomNumbers = await LoadRoomNumbersAsync(
            allListed.Where(reservation => reservation.RoomId is not null)
                .Select(reservation => reservation.RoomId!.Value)
                .Distinct()
                .ToArray(),
            cancellationToken);

        var customerNames = await LoadCustomerNamesAsync(
            allListed.Select(reservation => reservation.CustomerCode).ToArray(),
            cancellationToken);

        // L'occupation du jour reutilise exactement la logique d'occupation : un chiffre, une
        // definition.
        var occupancyResult = await GetOccupancyAsync(normalizedUnitCode, date, date, cancellationToken);

        if (!occupancyResult.Succeeded || occupancyResult.Value is null)
        {
            return MirrorFailure<OccupancyResponse, FrontDeskResponse>(
                occupancyResult,
                "L'occupation du jour n'a pas pu etre calculee.");
        }

        FrontDeskArrivalResponse MapArrival(Reservation reservation)
        {
            return new FrontDeskArrivalResponse(
                reservation.Id,
                reservation.CustomerCode,
                customerNames.GetValueOrDefault(reservation.CustomerCode),
                reservation.RoomId ?? Guid.Empty,
                reservation.RoomId is { } roomId ? roomNumbers.GetValueOrDefault(roomId) : null,
                reservation.ArrivalDate,
                reservation.DepartureDate,
                reservation.Nights,
                reservation.GuestCount,
                reservation.NightlyRateSnapshot,
                reservation.RatePlanCodeSnapshot,
                reservation.TotalStayAmount);
        }

        FrontDeskDepartureResponse MapDeparture(Reservation reservation)
        {
            return new FrontDeskDepartureResponse(
                reservation.Id,
                reservation.CustomerCode,
                customerNames.GetValueOrDefault(reservation.CustomerCode),
                reservation.RoomId ?? Guid.Empty,
                reservation.RoomId is { } roomId ? roomNumbers.GetValueOrDefault(roomId) : null,
                reservation.ArrivalDate,
                reservation.DepartureDate,
                reservation.Nights,
                reservation.GuestCount,
                folioBalances.TryGetValue(reservation.Id, out var balance) ? balance : null);
        }

        return ApplicationResult<FrontDeskResponse>.Success(new FrontDeskResponse(
            normalizedUnitCode,
            date,
            arrivals.Select(MapArrival).ToArray(),
            overdueArrivals.Select(MapArrival).ToArray(),
            departures.Select(MapDeparture).ToArray(),
            overdueDepartures.Select(MapDeparture).ToArray(),
            inHouseCount,
            occupancyResult.Value.Days.Single()));
    }
}
