namespace RaqmiSystem.Application.Lodging;

public sealed record ForecastResponse(
    string HotelUnitCode,
    DateOnly From,
    DateOnly To,
    int Days,
    IReadOnlyCollection<ForecastDayResponse> Entries,
    decimal AverageOccupancyPercent,
    decimal TotalRoomRevenue,
    decimal AverageAdr,
    decimal AverageRevPar);
