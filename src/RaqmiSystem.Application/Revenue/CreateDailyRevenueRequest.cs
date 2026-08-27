namespace RaqmiSystem.Application.Revenue;

public sealed record CreateDailyRevenueRequest(
    DateOnly BusinessDate,
    string HotelUnitCode,
    decimal Accommodation,
    decimal Food,
    decimal Beverage,
    decimal Other,
    string? Notes = null);
