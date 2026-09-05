namespace RaqmiSystem.Domain.Organization;

/// <summary>
/// Les paquets fonctionnels de Raqmi System : l'unité d'activation d'un ensemble de modules
/// pour une installation. Un paquet est une notion d'exécution, de navigation et de licence,
/// JAMAIS une notion de schéma : une seule base, une seule chaîne de migrations, un seul
/// binaire - le paquet décide de ce qui est proposé, pas de ce qui existe.
/// </summary>
/// <remarks>
/// L'énumération vit dans le domaine parce que le catalogue d'indicateurs, lui aussi dans le
/// domaine, rattache chaque définition à son paquet ; le manifeste complet (domaines, entrées
/// historiques, paquets actifs par secteur) est dans <c>Application/Navigation</c>. Les valeurs
/// numériques sont stables : elles finiront dans des fichiers de licence.
/// </remarks>
public enum ModulePack
{
    /// <summary>Toujours actif : administration, paramétrage, référentiel des établissements, audit, GED, contrôle interne.</summary>
    Core = 1,

    /// <summary>Recettes, trésorerie, comptabilité SCF, fiscalité, budget, créances.</summary>
    Finance = 2,

    /// <summary>Facturation et ventes.</summary>
    Sales = 3,

    /// <summary>Achats, fournisseurs, appels d'offres.</summary>
    Purchasing = 4,

    /// <summary>Stocks, dépôts, inventaires, consommations.</summary>
    Inventory = 5,

    /// <summary>Ressources humaines et paie.</summary>
    HumanResources = 6,

    /// <summary>Clients, segments, fidélité, satisfaction, campagnes.</summary>
    Crm = 7,

    /// <summary>Le vertical hôtelier : PMS, housekeeping, tarifs et distribution, marina.</summary>
    Hospitality = 8,

    /// <summary>Restauration : cuisine, fiches techniques, points de vente.</summary>
    FoodBeverage = 9,

    /// <summary>Groupes, MICE et événementiel.</summary>
    Events = 10,

    /// <summary>Toujours actif : tableaux de bord, bibliothèque KPI, rapports, comparatifs.</summary>
    Pilotage = 11,

    /// <summary>Toujours actif : sauvegardes, postes de travail, journalisation, intégrations matérielles.</summary>
    System = 12
}
