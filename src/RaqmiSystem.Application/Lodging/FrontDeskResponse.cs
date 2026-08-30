namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// The front-desk counter screen for one unit and one business day, in one call:
/// today's expected arrivals (Booked, arriving that day), today's departures (CheckedIn,
/// leaving that day, with folio balances), the overdue lists real PMSs pin on top -
/// <see cref="OverdueArrivals"/> (Booked stays whose arrival date is past: no-show candidates)
/// and <see cref="OverdueDepartures"/> (CheckedIn stays whose departure date is past: late
/// check-outs, with balances) - plus the in-house count for the night and the day's occupancy.
/// </summary>
public sealed record FrontDeskResponse(
    string HotelUnitCode,
    DateOnly Date,
    IReadOnlyCollection<FrontDeskArrivalResponse> Arrivals,
    IReadOnlyCollection<FrontDeskArrivalResponse> OverdueArrivals,
    IReadOnlyCollection<FrontDeskDepartureResponse> Departures,
    IReadOnlyCollection<FrontDeskDepartureResponse> OverdueDepartures,
    int InHouseCount,
    OccupancyDayResponse Occupancy);
