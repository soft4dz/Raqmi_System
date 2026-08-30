namespace RaqmiSystem.Application.Tariffs;

public sealed record CreateRatePeriodRequest(
    string RoomTypeCode,
    DateOnly FromDate,
    DateOnly ToDate,
    decimal NightlyAmount);
