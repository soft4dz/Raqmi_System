namespace RaqmiSystem.Application.Tariffs;

public sealed record RatePlanResponse(
    Guid Id,
    string Code,
    string Label,
    string HotelUnitCode,
    bool IsDefault,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
