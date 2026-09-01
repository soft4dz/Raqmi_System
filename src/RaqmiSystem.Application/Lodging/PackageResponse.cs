namespace RaqmiSystem.Application.Lodging;

public sealed record PackageResponse(
    Guid Id,
    string HotelUnitCode,
    string Code,
    string Label,
    string? Description,
    decimal TotalPrice,
    decimal ComponentsTotal,
    bool IsBalanced,
    string? RatePlanCode,
    string? RoomTypeCode,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    int Nights,
    bool IsActive,
    IReadOnlyCollection<PackageComponentResponse> Components,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
