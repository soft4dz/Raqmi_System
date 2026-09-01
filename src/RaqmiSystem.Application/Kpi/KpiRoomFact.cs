namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Une chambre du referentiel. <paramref name="IsActive"/> separe le parc BATI (toutes les
/// chambres) du parc EXPLOITE (celles qu'on peut vendre) : une chambre desactivee n'entre ni
/// dans les nuitees disponibles ni dans l'occupation, elle n'existe commercialement plus.
/// </summary>
public sealed record KpiRoomFact(string HotelUnitCode, Guid RoomId, bool IsActive);
