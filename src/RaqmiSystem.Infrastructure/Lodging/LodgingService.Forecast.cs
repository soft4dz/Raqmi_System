using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Infrastructure.Lodging;

/// <summary>
/// Le previsionnel d'occupation et le planning graphique.
/// </summary>
public sealed partial class LodgingService
{
    public async Task<ApplicationResult<ForecastResponse>> GetForecastAsync(
        string hotelUnitCode,
        DateOnly from,
        int days,
        CancellationToken cancellationToken)
    {
        if (days <= 0)
        {
            return ApplicationResult<ForecastResponse>.Validation(
                "Le previsionnel doit couvrir au moins une journee.");
        }

        if (days > MaxForecastDays)
        {
            return ApplicationResult<ForecastResponse>.Validation(
                $"Le previsionnel ne peut pas depasser {MaxForecastDays} jours.");
        }

        var normalizedUnitCode = NormalizeCodeOrEmpty(hotelUnitCode);

        var unitFailure = await RequireHotelUnitAsync<ForecastResponse>(normalizedUnitCode, cancellationToken);

        if (unitFailure is not null)
        {
            return unitFailure;
        }

        var to = from.AddDays(days);
        var policy = await GetPolicyEntityAsync(normalizedUnitCode, cancellationToken);

        var physicalRooms = await dbContext.Set<Room>()
            .AsNoTracking()
            .CountAsync(room => room.HotelUnitCode == normalizedUnitCode && room.IsActive, cancellationToken);

        var blocked = await GetBlockedRoomCountsAsync(normalizedUnitCode, null, from, to, policy, cancellationToken);

        // Le previsionnel lit les MEMES sejours que la disponibilite. Ils sont charges une fois et
        // parcourus en memoire : le detail des tarifs figes vit dans un JSON par dossier, il ne se
        // somme pas en SQL.
        var reservations = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => reservation.HotelUnitCode == normalizedUnitCode)
            .Where(BlocksPeriod(from, to))
            .ToArrayAsync(cancellationToken);

        var roomTypeCodes = await dbContext.Set<RoomType>()
            .AsNoTracking()
            .Where(roomType => roomType.HotelUnitCode == normalizedUnitCode && roomType.IsActive)
            .Select(roomType => roomType.Code)
            .ToArrayAsync(cancellationToken);

        var holds = new Dictionary<DateOnly, int>();
        var overbooking = new Dictionary<DateOnly, int>();

        foreach (var roomTypeCode in roomTypeCodes)
        {
            var typeHolds = await GetAllotmentHoldsAsync(normalizedUnitCode, roomTypeCode, from, to, cancellationToken);

            foreach (var entry in typeHolds)
            {
                holds[entry.Key] = holds.GetValueOrDefault(entry.Key) + entry.Value;
            }

            var typeOverbooking = await GetOverbookingAsync(
                normalizedUnitCode,
                roomTypeCode,
                from,
                to,
                policy,
                cancellationToken);

            foreach (var entry in typeOverbooking)
            {
                overbooking[entry.Key] = overbooking.GetValueOrDefault(entry.Key) + entry.Value;
            }
        }

        // Les tarifs figes sont deserialises une seule fois par dossier : les relire par nuit
        // ferait N x M analyses JSON sur un previsionnel a 365 jours.
        var ratesByReservation = reservations.ToDictionary(
            reservation => reservation.Id,
            reservation => reservation.GetNightlyRates().ToDictionary(rate => rate.Night, rate => rate.Amount));

        var entries = new List<ForecastDayResponse>(days);

        for (var day = from; day < to; day = day.AddDays(1))
        {
            var night = day;

            var covering = reservations.Where(reservation => reservation.CoversNight(night)).ToArray();
            var soldRooms = covering.Length;
            var arrivals = covering.Count(reservation => reservation.ArrivalDate == night);
            var departures = reservations.Count(reservation => reservation.DepartureDate == night);

            // Stay-over : le client dort ici cette nuit ET la nuit precedente. C'est ce chiffre, et
            // non "vendu moins arrivees", qui dit combien de chambres n'auront pas a etre remises.
            var stayOvers = covering.Count(reservation => reservation.CoversNight(night.AddDays(-1)));

            var sellable = Math.Max(0, physicalRooms - blocked.Total.GetValueOrDefault(night));
            var heldRooms = holds.GetValueOrDefault(night);
            var remaining = Math.Max(0, sellable - soldRooms - heldRooms);
            var overbookingUsed = Math.Max(0, soldRooms - sellable);

            var roomRevenue = covering.Sum(reservation =>
                ratesByReservation[reservation.Id].GetValueOrDefault(night));

            var occupancy = sellable == 0
                ? 0m
                : Math.Round((decimal)soldRooms * 100m / sellable, 2, MidpointRounding.AwayFromZero);

            // ADR = prix moyen de la nuitee VENDUE ; RevPAR = revenu par chambre DISPONIBLE. C'est
            // le RevPAR qui dit si l'hotel remplit bien : l'ADR seul peut monter pendant que
            // l'etablissement se vide.
            var adr = soldRooms == 0
                ? 0m
                : Math.Round(roomRevenue / soldRooms, 2, MidpointRounding.AwayFromZero);

            var revPar = sellable == 0
                ? 0m
                : Math.Round(roomRevenue / sellable, 2, MidpointRounding.AwayFromZero);

            entries.Add(new ForecastDayResponse(
                night,
                physicalRooms,
                blocked.OutOfOrder.GetValueOrDefault(night),
                blocked.OutOfService.GetValueOrDefault(night),
                sellable,
                heldRooms,
                soldRooms,
                arrivals,
                departures,
                stayOvers,
                remaining,
                overbooking.GetValueOrDefault(night),
                overbookingUsed,
                occupancy,
                roomRevenue,
                adr,
                revPar,
                covering.Sum(reservation => reservation.GuestCount)));
        }

        var totalRevenue = entries.Sum(entry => entry.RoomRevenue);
        var totalSold = entries.Sum(entry => entry.SoldRooms);
        var totalSellable = entries.Sum(entry => entry.SellableRooms);

        return ApplicationResult<ForecastResponse>.Success(new ForecastResponse(
            normalizedUnitCode,
            from,
            to.AddDays(-1),
            days,
            entries,
            entries.Count == 0 ? 0m : Math.Round(entries.Average(entry => entry.OccupancyPercent), 2, MidpointRounding.AwayFromZero),
            totalRevenue,
            totalSold == 0 ? 0m : Math.Round(totalRevenue / totalSold, 2, MidpointRounding.AwayFromZero),
            totalSellable == 0 ? 0m : Math.Round(totalRevenue / totalSellable, 2, MidpointRounding.AwayFromZero)));
    }

    // ==================================== Planning graphique ====================================

    public async Task<ApplicationResult<TapeChartResponse>> GetTapeChartAsync(
        string hotelUnitCode,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        if (to <= from)
        {
            return ApplicationResult<TapeChartResponse>.Validation(
                "La date de fin doit etre posterieure a la date de debut.");
        }

        if (to.DayNumber - from.DayNumber > MaxOccupancyWindowDays)
        {
            return ApplicationResult<TapeChartResponse>.Validation(
                $"Le planning ne peut pas depasser {MaxOccupancyWindowDays} jours.");
        }

        var normalizedUnitCode = NormalizeCodeOrEmpty(hotelUnitCode);

        var unitFailure = await RequireHotelUnitAsync<TapeChartResponse>(normalizedUnitCode, cancellationToken);

        if (unitFailure is not null)
        {
            return unitFailure;
        }

        var policy = await GetPolicyEntityAsync(normalizedUnitCode, cancellationToken);

        var rooms = await (
            from room in dbContext.Set<Room>().AsNoTracking()
            where room.HotelUnitCode == normalizedUnitCode && room.IsActive
            join roomType in dbContext.Set<RoomType>().AsNoTracking()
                on new { Unit = room.HotelUnitCode, Code = room.RoomTypeCode }
                equals new { Unit = roomType.HotelUnitCode, Code = roomType.Code }
            orderby roomType.Rank, room.DisplayOrder, room.Number
            select new
            {
                room.Id,
                room.Number,
                RoomTypeCode = roomType.Code,
                RoomTypeLabel = roomType.Label,
                room.Floor,
                room.Building,
                room.IsActive
            })
            .ToArrayAsync(cancellationToken);

        var reservations = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => reservation.HotelUnitCode == normalizedUnitCode)
            .Where(BlocksPeriod(from, to))
            .ToArrayAsync(cancellationToken);

        var blocks = await dbContext.Set<RoomBlock>()
            .AsNoTracking()
            .Where(block => block.HotelUnitCode == normalizedUnitCode
                && block.Status != RoomBlockStatus.Cancelled
                && block.StartDate < to
                && block.EndDate > from)
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

        var customerNames = await LoadCustomerNamesAsync(
            reservations.Select(reservation => reservation.CustomerCode).ToArray(),
            cancellationToken);

        var conditions = await LoadRoomConditionsAsync(normalizedUnitCode, cancellationToken);

        TapeChartBarResponse MapReservationBar(Reservation reservation)
        {
            return new TapeChartBarResponse(
                "Reservation",
                reservation.Id,
                null,
                reservation.Number,
                reservation.GuestName ?? customerNames.GetValueOrDefault(reservation.CustomerCode),
                reservation.Status.ToString(),
                reservation.ArrivalDate,
                reservation.DepartureDate,
                reservation.Nights,
                reservation.CustomerCode,
                customerNames.GetValueOrDefault(reservation.CustomerCode),
                reservation.RoomTypeCode,
                reservation.GuestCount,
                reservation.TotalStayAmount,
                balances.GetValueOrDefault(reservation.Id),
                reservation.IsOverbooking,
                StatusColour(reservation.Status, reservation.IsOverbooking));
        }

        var rows = rooms.Select(room => new TapeChartRowResponse(
            room.Id,
            room.Number,
            room.RoomTypeCode,
            room.RoomTypeLabel,
            room.Floor,
            room.Building,
            room.IsActive,
            conditions.TryGetValue(room.Id, out var status)
                ? status.ToString()
                : nameof(Domain.Housekeeping.RoomConditionStatus.Clean),
            reservations
                .Where(reservation => reservation.RoomId == room.Id)
                .Select(MapReservationBar)
                .Concat(blocks
                    .Where(block => block.RoomId == room.Id)
                    .Select(block => new TapeChartBarResponse(
                        block.Kind.ToString(),
                        null,
                        block.Id,
                        null,
                        block.Reason,
                        block.Status.ToString(),
                        block.StartDate,
                        block.ActualReturnDate ?? block.EndDate,
                        block.Nights,
                        null,
                        null,
                        room.RoomTypeCode,
                        0,
                        null,
                        null,
                        false,
                        block.Kind == RoomBlockKind.OutOfOrder ? "#B00020" : "#8A6D3B")))
                .OrderBy(bar => bar.From)
                .ToArray()))
            .ToArray();

        // LES SEJOURS SANS CHAMBRE AFFECTEE SONT RENDUS A PART, et c'est essentiel : ils n'ont
        // aucune ligne sur le plan et pourtant ils CONSOMMENT l'inventaire. Les omettre ferait
        // croire a des chambres libres qui sont deja vendues - la facon la plus courante dont un
        // tape chart fait survendre un hotel.
        var unassigned = reservations
            .Where(reservation => reservation.RoomId is null)
            .Select(MapReservationBar)
            .OrderBy(bar => bar.From)
            .ToArray();

        var inventory = new List<NightInventoryResponse>();

        foreach (var roomTypeCode in rooms.Select(room => room.RoomTypeCode).Distinct())
        {
            var availability = await BuildRoomTypeAvailabilityAsync(
                normalizedUnitCode,
                roomTypeCode,
                from,
                to,
                policy,
                excludeReservationId: null,
                cancellationToken);

            foreach (var night in availability.Nights)
            {
                var existing = inventory.FindIndex(entry => entry.Night == night.Night);

                var mapped = new NightInventoryResponse(
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
                    false);

                if (existing < 0)
                {
                    inventory.Add(mapped);
                }
                else
                {
                    var current = inventory[existing];

                    inventory[existing] = current with
                    {
                        PhysicalRooms = current.PhysicalRooms + mapped.PhysicalRooms,
                        BlockedRooms = current.BlockedRooms + mapped.BlockedRooms,
                        SoldRooms = current.SoldRooms + mapped.SoldRooms,
                        AllotmentHolds = current.AllotmentHolds + mapped.AllotmentHolds,
                        OverbookingAllowed = current.OverbookingAllowed + mapped.OverbookingAllowed,
                        OverbookingUsed = current.OverbookingUsed + mapped.OverbookingUsed,
                        SellableCapacity = current.SellableCapacity + mapped.SellableCapacity,
                        PhysicalAvailable = current.PhysicalAvailable + mapped.PhysicalAvailable,
                        PublicAvailable = current.PublicAvailable + mapped.PublicAvailable,
                        CommercialAvailable = current.CommercialAvailable + mapped.CommercialAvailable,
                        OccupancyPercent = current.SellableCapacity + mapped.SellableCapacity == 0
                            ? 0m
                            : Math.Round(
                                (decimal)(current.SoldRooms + mapped.SoldRooms) * 100m
                                    / (current.SellableCapacity + mapped.SellableCapacity),
                                2,
                                MidpointRounding.AwayFromZero)
                    };
                }
            }
        }

        return ApplicationResult<TapeChartResponse>.Success(new TapeChartResponse(
            normalizedUnitCode,
            from,
            to,
            rows,
            unassigned,
            inventory.OrderBy(entry => entry.Night).ToArray()));
    }

    /// <summary>
    /// La couleur d'un bloc du planning. Elle n'est pas decorative : au comptoir, le plan se lit de
    /// loin, et une surreservation ou un dossier non garanti doit sauter aux yeux.
    /// </summary>
    private static string StatusColour(ReservationStatus status, bool isOverbooking)
    {
        if (isOverbooking)
        {
            return "#C2410C";
        }

        return status switch
        {
            ReservationStatus.Option => "#A16207",
            ReservationStatus.Confirmed => "#1D4ED8",
            ReservationStatus.Guaranteed => "#15803D",
            ReservationStatus.CheckedIn => "#0F766E",
            ReservationStatus.CheckedOut => "#6B7280",
            _ => "#9CA3AF"
        };
    }
}
