namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Un bloc du planning. <paramref name="Kind"/> vaut "Reservation", "OutOfOrder", "OutOfService"
/// ou "Allotment" : les quatre choses qui peuvent occuper une ligne de plan, et qui ne se
/// deplacent pas de la meme facon.
/// </summary>
public sealed record TapeChartBarResponse(
    string Kind,
    Guid? ReservationId,
    Guid? BlockId,
    string? Number,
    string? Label,
    string? Status,
    DateOnly From,
    DateOnly To,
    int Nights,
    string? CustomerCode,
    string? CustomerName,
    string? RoomTypeCode,
    int GuestCount,
    decimal? TotalAmount,
    decimal? Balance,
    bool IsOverbooking,
    string? Colour);
