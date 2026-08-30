namespace RaqmiSystem.Application.Tariffs;

public sealed record RatePeriodResponse(
    Guid Id,
    string RatePlanCode,
    string RoomTypeCode,
    DateOnly FromDate,
    DateOnly ToDate,
    decimal NightlyAmount,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
