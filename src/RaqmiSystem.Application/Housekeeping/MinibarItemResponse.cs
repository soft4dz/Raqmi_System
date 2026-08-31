namespace RaqmiSystem.Application.Housekeeping;

public sealed record MinibarItemResponse(
    Guid Id,
    string HotelUnitCode,
    string Code,
    string Label,
    decimal UnitPrice,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
