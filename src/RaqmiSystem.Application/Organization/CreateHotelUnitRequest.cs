using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Application.Organization;

/// <param name="Sector">
/// Optionnel pour la compatibilité des clients existants : absent, l'établissement est
/// hôtelier - ce que tout appelant antérieur à la notion créait sans le dire.
/// </param>
public sealed record CreateHotelUnitRequest(
    string Code,
    string Name,
    HotelUnitType UnitType,
    int DisplayOrder = 0,
    BusinessSector? Sector = null);
