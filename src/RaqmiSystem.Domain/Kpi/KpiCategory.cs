namespace RaqmiSystem.Domain.Kpi;

/// <summary>
/// Famille metier d'un indicateur. Elle sert au regroupement des ecrans et au filtrage des
/// endpoints ; elle ne porte AUCUNE regle de calcul et ne commande pas la permission exigee -
/// celle-ci vient du module qui possede la donnee source (voir
/// <see cref="KpiDefinition.RequiredPermission"/>).
/// </summary>
public enum KpiCategory
{
    /// <summary>Hebergement : occupation, ADR, RevPAR, sejours, annulations.</summary>
    Accommodation = 1,

    /// <summary>Finance : chiffre d'affaires, resultat, marges, tresorerie, creances.</summary>
    Finance = 2,

    /// <summary>Restauration et boissons : cout matiere, stocks consommes.</summary>
    FoodBeverage = 3,

    /// <summary>Ressources humaines : masse salariale, absenteisme, turnover, productivite.</summary>
    HumanResources = 4,

    /// <summary>Maintenance des equipements : MTTR, MTBF, preventif, cout par equipement.</summary>
    Maintenance = 5,

    /// <summary>Experience client : satisfaction, NPS, fidelisation.</summary>
    GuestExperience = 6,

    /// <summary>Achats et stocks : rotation, ruptures, ecarts de prix.</summary>
    SupplyChain = 7
}
