using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Application.Revenue;

/// <summary>
/// Création d'une catégorie. Le secteur cible est optionnel : sans secteur, la catégorie rejoint
/// le jeu générique, proposé aux seuls secteurs qui n'ont pas de jeu dédié (voir
/// <see cref="RevenueCategoryCatalog"/>).
/// </summary>
public sealed record CreateRevenueCategoryRequest(
    string Code,
    string Label,
    int DisplayOrder = 0,
    BusinessSector? Sector = null);
