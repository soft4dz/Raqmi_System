namespace RaqmiSystem.Application.Lodging;

public sealed record OccupancyResponse(
    string HotelUnitCode,
    DateOnly From,
    DateOnly To,
    IReadOnlyCollection<OccupancyDayResponse> Days);
