namespace RaqmiSystem.Domain.Tariffs;

/// <summary>
/// Pension comprise dans un plan tarifaire. Ce n'est pas une etiquette commerciale : c'est ce qui
/// dit au night audit quelles prestations poser automatiquement chaque nuit, et au restaurant
/// combien de couverts attendre le lendemain matin.
/// </summary>
public enum BoardType
{
    /// <summary>Chambre seule.</summary>
    RoomOnly = 0,

    /// <summary>Chambre et petit-dejeuner.</summary>
    BedAndBreakfast = 1,

    /// <summary>Demi-pension : petit-dejeuner et un repas.</summary>
    HalfBoard = 2,

    /// <summary>Pension complete : petit-dejeuner et deux repas.</summary>
    FullBoard = 3,

    /// <summary>Tout compris.</summary>
    AllInclusive = 4
}
