namespace RaqmiSystem.Application.Housekeeping;

/// <summary>
/// A recorded minibar consumption. Code, label and unit price are the frozen snapshot of the
/// price list at recording time, not a live lookup: this is what the guest was charged.
/// </summary>
public sealed record MinibarConsumptionResponse(
    Guid Id,
    string HotelUnitCode,
    Guid RoomId,
    string RoomNumber,
    Guid ReservationId,
    string ItemCode,
    string ItemLabel,
    decimal UnitPrice,
    int Quantity,
    decimal TotalAmount,
    DateOnly ConsumedOn,
    string? Notes,
    DateTimeOffset CreatedAt,
    string CreatedBy);
