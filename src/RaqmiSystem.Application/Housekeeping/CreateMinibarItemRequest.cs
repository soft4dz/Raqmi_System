namespace RaqmiSystem.Application.Housekeeping;

public sealed record CreateMinibarItemRequest(
    string HotelUnitCode,
    string Code,
    string Label,
    decimal UnitPrice);
