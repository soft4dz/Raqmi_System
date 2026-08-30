namespace RaqmiSystem.Application.Tariffs;

public sealed record UpdateRatePeriodRequest(
    DateOnly FromDate,
    DateOnly ToDate,
    decimal NightlyAmount);
