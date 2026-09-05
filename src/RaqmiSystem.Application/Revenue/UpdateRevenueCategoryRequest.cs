using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Application.Revenue;

/// <summary>
/// Mise à jour d'une catégorie. Le code ne se modifie pas : il est référencé par les lignes de
/// recettes et de budget déjà enregistrées. Une catégorie qui ne sert plus se désactive
/// (<see cref="IsActive"/>) : les recettes passées la gardent, les nouvelles ne la proposent plus.
/// </summary>
public sealed record UpdateRevenueCategoryRequest(
    string Label,
    int DisplayOrder,
    BusinessSector? Sector,
    bool IsActive);
