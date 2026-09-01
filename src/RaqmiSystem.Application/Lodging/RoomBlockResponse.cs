using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Un blocage de chambre tel que l'ecran le lit : la chambre, la nature du retrait, la periode
/// prevue, la date reelle de retour quand elle est connue, et le motif.
/// </summary>
public sealed record RoomBlockResponse(
    Guid Id,
    string HotelUnitCode,
    Guid RoomId,
    string? RoomNumber,
    string? RoomTypeCode,
    RoomBlockKind Kind,
    RoomBlockCategory Category,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly? ActualReturnDate,
    int Nights,
    string Reason,
    string? MaintenanceReference,
    string? Comment,
    RoomBlockStatus Status,
    bool ReducesSellableInventory,
    DateTimeOffset? ClosedAt,
    string? ClosedBy,
    string? CancelReason,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
