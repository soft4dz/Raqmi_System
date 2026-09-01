using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Retrait d'une chambre de l'exploitation. <paramref name="EndDate"/> est EXCLUE : un blocage du
/// 10 au 12 immobilise les nuits du 10 et du 11, et la chambre est revendable des la nuit du 12.
/// </summary>
public sealed record CreateRoomBlockRequest(
    Guid RoomId,
    RoomBlockKind Kind,
    DateOnly StartDate,
    DateOnly EndDate,
    string Reason,
    RoomBlockCategory Category = RoomBlockCategory.Unspecified,
    string? MaintenanceReference = null,
    string? Comment = null);
