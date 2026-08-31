namespace RaqmiSystem.Application.Mice;

/// <summary>
/// Modifie un bloc. Reduire le nombre de chambres tenues en dessous de ce qui est deja pris est
/// refuse : le bloc ne peut pas devenir plus petit que sa propre consommation.
/// </summary>
public sealed record UpdateRoomAllotmentRequest(
    DateOnly ArrivalDate,
    DateOnly DepartureDate,
    int RoomsHeld,
    DateOnly? ReleaseDate,
    string? Notes);
