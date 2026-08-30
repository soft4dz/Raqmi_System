namespace RaqmiSystem.Application.Tariffs;

public sealed record CreateRatePlanRequest(
    string Code,
    string Label,
    string HotelUnitCode,
    bool IsDefault);
