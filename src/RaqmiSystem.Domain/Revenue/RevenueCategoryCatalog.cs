using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Revenue;

/// <summary>
/// Jeu de catégories livré d'origine et règle de sélection par secteur.
///
/// Deux jeux : le jeu hôtelier (les quatre anciennes colonnes, ciblées Hospitality) et le jeu
/// générique (sans secteur cible). La règle qui les départage est la suivante : <b>une catégorie
/// sans secteur appartient au jeu générique, et le jeu générique ne s'applique qu'aux secteurs
/// qui n'ont pas de jeu dédié</b>. Un hôtel voit donc exactement ses quatre catégories — son
/// écran de saisie ne change pas — et un négociant voit les trois génériques, sans qu'aucune
/// activation manuelle ne soit nécessaire dans un cas comme dans l'autre. Corollaire à connaître
/// pour qui paramètre : une catégorie ajoutée pour un hôtel doit cibler Hospitality, sinon elle
/// rejoint le jeu générique et n'est pas proposée à l'hôtel.
/// </summary>
public static class RevenueCategoryCatalog
{
    public static IReadOnlyList<RevenueCategory> Hotel { get; } =
    [
        new RevenueCategory(RevenueCategoryCodes.Accommodation, "Hébergement", 10, BusinessSector.Hospitality),
        new RevenueCategory(RevenueCategoryCodes.Food, "Restauration", 20, BusinessSector.Hospitality),
        new RevenueCategory(RevenueCategoryCodes.Beverage, "Bar", 30, BusinessSector.Hospitality),
        new RevenueCategory(RevenueCategoryCodes.Other, "Autres", 40, BusinessSector.Hospitality)
    ];

    public static IReadOnlyList<RevenueCategory> Generic { get; } =
    [
        new RevenueCategory(RevenueCategoryCodes.Merchandise, "Ventes de marchandises", 110),
        new RevenueCategory(RevenueCategoryCodes.Services, "Prestations de services", 120),
        new RevenueCategory(RevenueCategoryCodes.OtherIncome, "Autres produits", 130)
    ];

    public static IReadOnlyList<RevenueCategory> All { get; } = [.. Hotel, .. Generic];

    /// <summary>Le jeu par défaut d'un secteur, avant tout paramétrage.</summary>
    public static IReadOnlyList<RevenueCategory> DefaultsFor(BusinessSector? sector)
    {
        return Applicable(All, sector);
    }

    /// <summary>
    /// Applique la règle « jeu dédié, sinon jeu générique » à une liste quelconque de
    /// catégories (celles de la base, par exemple), dans l'ordre d'affichage.
    /// </summary>
    public static IReadOnlyList<RevenueCategory> Applicable(IEnumerable<RevenueCategory> categories, BusinessSector? sector)
    {
        ArgumentNullException.ThrowIfNull(categories);

        var ordered = categories
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.Code, StringComparer.Ordinal)
            .ToArray();

        var dedicated = sector.HasValue
            ? ordered.Where(category => category.Sector == sector.Value).ToArray()
            : [];

        return dedicated.Length > 0
            ? dedicated
            : ordered.Where(category => category.Sector is null).ToArray();
    }

    /// <summary>
    /// Secteur d'une unité d'exploitation. Tous les types d'unité connus sont de l'hôtellerie :
    /// le jour où l'établissement portera son propre secteur, c'est ici — et seulement ici — que
    /// la correspondance changera.
    /// </summary>
    public static BusinessSector SectorOf(HotelUnitType unitType)
    {
        return BusinessSector.Hospitality;
    }
}
