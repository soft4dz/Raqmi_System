namespace RaqmiSystem.Application.Lodging;

public sealed record DepartureBoardResponse(
    string HotelUnitCode,
    DateOnly BusinessDate,
    IReadOnlyCollection<DepartureRowResponse> Departures,
    int PendingCount,
    decimal OutstandingBalance);
