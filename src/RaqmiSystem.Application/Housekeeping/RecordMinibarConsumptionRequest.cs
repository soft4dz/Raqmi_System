namespace RaqmiSystem.Application.Housekeeping;

/// <summary>
/// One minibar consumption to record and bill. The reservation must be checked in, the item must
/// belong to the same unit and be active, and the price applied is the one on the price list at
/// that moment - frozen into the consumption row and onto the folio line it produces.
/// ConsumedOn defaults to the current business day when it is omitted.
/// </summary>
public sealed record RecordMinibarConsumptionRequest(
    Guid ReservationId,
    string ItemCode,
    int Quantity,
    DateOnly? ConsumedOn = null,
    string? Notes = null);
