namespace RaqmiSystem.Domain.Organization;

/// <summary>
/// Le secteur d'activité d'un établissement : ce qui décide des paquets fonctionnels proposés
/// par défaut (voir <c>ModulePackCatalog.ActivePacksFor</c>). Un hôtel et un commerce
/// partagent le socle - comptabilité, trésorerie, achats, paie - mais pas le PMS ni la cuisine.
/// </summary>
/// <remarks>
/// Le secteur est déclaré, jamais déduit du type d'établissement : une résidence peut être
/// gérée comme un bien immobilier (services) et un restaurant d'entreprise comme un service
/// interne. Les valeurs numériques sont stables ; la base stocke le nom.
/// </remarks>
public enum BusinessSector
{
    /// <summary>Hôtellerie, résidences, plages, marinas, restauration commerciale.</summary>
    Hospitality = 1,

    /// <summary>Commerce de détail et de gros : boutiques, magasins, entrepôts de distribution.</summary>
    Retail = 2,

    /// <summary>Services : cabinets, agences, bureaux, prestations intellectuelles.</summary>
    Services = 3,

    /// <summary>Industrie et production : ateliers, usines, logistique amont.</summary>
    Manufacturing = 4,

    /// <summary>Enseignement et formation : écoles, instituts, centres de formation.</summary>
    Education = 5,

    /// <summary>Santé : cliniques, cabinets médicaux, laboratoires.</summary>
    Health = 6,

    /// <summary>Tout autre secteur : le socle générique, sans vertical.</summary>
    Other = 99
}
