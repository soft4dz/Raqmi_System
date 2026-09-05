using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Application.Organization;

/// <param name="Sector">
/// Optionnel : absent, le secteur de l'établissement est laissé tel quel. Un client qui ne
/// connaît pas encore la notion corrige un libellé sans remettre l'unité à l'hôtellerie.
/// </param>
public sealed record UpdateHotelUnitRequest(
    string Name,
    HotelUnitType UnitType,
    int DisplayOrder = 0,
    BusinessSector? Sector = null);
