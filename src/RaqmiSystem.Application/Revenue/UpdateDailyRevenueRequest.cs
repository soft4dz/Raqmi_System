namespace RaqmiSystem.Application.Revenue;

public sealed record UpdateDailyRevenueRequest(
    decimal Accommodation,
    decimal Food,
    decimal Beverage,
    decimal Other,
    string? Notes = null);
