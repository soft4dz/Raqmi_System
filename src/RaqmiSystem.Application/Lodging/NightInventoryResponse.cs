namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// L'inventaire d'une nuit, decompose. Chaque colonne repond a une question distincte, et l'ordre
/// des soustractions est celui du domaine :
/// parc - blocages = capacite vendable ; - vendu = disponible physique ; - allotements =
/// disponible public ; + solde de surreservation = disponible commercial.
/// </summary>
public sealed record NightInventoryResponse(
    DateOnly Night,
    int PhysicalRooms,
    int BlockedRooms,
    int SoldRooms,
    int AllotmentHolds,
    int OverbookingAllowed,
    int OverbookingUsed,
    int SellableCapacity,
    int PhysicalAvailable,
    int PublicAvailable,
    int CommercialAvailable,
    decimal OccupancyPercent,
    bool IsClosed);
