using System.Collections.Frozen;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Application.Navigation;

/// <summary>
/// Le manifeste des paquets fonctionnels : quels paquets existent, à quel paquet appartient
/// chacun des 22 domaines et chacune des 50 entrées historiques, et quels paquets une
/// installation active par défaut selon son secteur d'activité.
/// </summary>
/// <remarks>
/// Un paquet n'est jamais une notion de schéma (une base, une chaîne de migrations, un
/// binaire) : il décide de ce que la navigation propose, de ce que le catalogue KPI liste
/// (<see cref="RaqmiSystem.Domain.Kpi.KpiCatalog.ForPacks"/>) et, demain, de ce qu'une
/// licence autorise. Le rattachement se fait par DOMAINE, avec des exceptions nommées par
/// entrée historique : un domaine transverse (Juridique &amp; Conformité) peut abriter une
/// entrée purement hôtelière (Conformité hôtelière).
///
/// Le filtre de licence de <see cref="NavigationTreeBuilder"/> ne lit qu'une chaîne
/// (<see cref="INavigationNode.LicenseFeature"/>) : <see cref="LicenseFeatureFor"/> la
/// fabrique, <see cref="LicenseFilter"/> la juge. Le câblage - poser la chaîne sur les nœuds
/// de domaine, passer le filtre dans <see cref="NavigationFilter.LicenseAllows"/> - est laissé
/// à l'intégrateur, hors de ce fichier.
/// </remarks>
public static class ModulePackCatalog
{
    /// <summary>Préfixe des chaînes de licence produites par ce manifeste (« pack:hospitality »).</summary>
    public const string LicenseFeaturePrefix = "pack:";

    public static IReadOnlyList<ModulePackDefinition> Definitions { get; } =
    [
        new(ModulePack.Core, "Socle",
            "Administration, paramétrage, référentiel des établissements, audit, circuits de validation, GED, contrôle interne.", AlwaysActive: true),
        new(ModulePack.Finance, "Finance",
            "Recettes, trésorerie, comptabilité SCF, fiscalité, budget, créances et recouvrement.", AlwaysActive: false),
        new(ModulePack.Sales, "Ventes",
            "Facturation et ventes.", AlwaysActive: false),
        new(ModulePack.Purchasing, "Achats",
            "Fournisseurs, commandes, réceptions, appels d'offres.", AlwaysActive: false),
        new(ModulePack.Inventory, "Stocks",
            "Dépôts, mouvements valorisés, inventaires, consommations.", AlwaysActive: false),
        new(ModulePack.HumanResources, "Ressources humaines",
            "Dossiers du personnel, contrats, paie.", AlwaysActive: false),
        new(ModulePack.Crm, "Relation client",
            "Fichier clients, segments, fidélité, satisfaction, campagnes, réclamations.", AlwaysActive: false),
        new(ModulePack.Hospitality, "Hôtellerie",
            "PMS et front office, housekeeping, tarifs et distribution, conformité hôtelière, marina.", AlwaysActive: false),
        new(ModulePack.FoodBeverage, "Restauration",
            "Cuisine, fiches techniques, coût matière, points de vente.", AlwaysActive: false),
        new(ModulePack.Events, "Événementiel",
            "Groupes, MICE, séminaires et banquets.", AlwaysActive: false),
        new(ModulePack.Pilotage, "Pilotage",
            "Tableaux de bord, bibliothèque KPI, rapports, comparatif inter-unités.", AlwaysActive: true),
        new(ModulePack.System, "Système",
            "Sauvegardes, postes de travail, journalisation, intégrations matérielles.", AlwaysActive: true)
    ];

    /// <summary>Les paquets qu'aucune installation ne peut désactiver.</summary>
    public static IReadOnlySet<ModulePack> AlwaysActivePacks { get; } =
        Definitions.Where(definition => definition.AlwaysActive).Select(definition => definition.Pack).ToFrozenSet();

    // Rattachement primaire : un domaine, un paquet. Les identifiants sont ceux de
    // FunctionalArchitectureCatalog.Domains ; le constructeur statique vérifie la couverture.
    private static readonly FrozenDictionary<string, ModulePack> PackByDomain = new Dictionary<string, ModulePack>(StringComparer.Ordinal)
    {
        ["01"] = ModulePack.Core,            // Mon Espace
        ["02"] = ModulePack.Core,            // Administration & Socle ERP
        ["03"] = ModulePack.Finance,         // Finance & Comptabilité
        ["04"] = ModulePack.Crm,             // Commercial, Clients & CRM
        ["05"] = ModulePack.Sales,           // Facturation & Ventes
        ["06"] = ModulePack.Hospitality,     // PMS / Hébergement
        ["07"] = ModulePack.Hospitality,     // Revenue Management & Distribution
        ["08"] = ModulePack.Hospitality,     // Housekeeping
        ["09"] = ModulePack.Events,          // Groupes, MICE & Événementiel
        ["10"] = ModulePack.FoodBeverage,    // F&B / Restauration
        ["11"] = ModulePack.Inventory,       // Stocks & Économat
        ["12"] = ModulePack.Purchasing,      // Achats & Fournisseurs
        ["13"] = ModulePack.HumanResources,  // Ressources Humaines & Paie
        ["14"] = ModulePack.Core,            // Maintenance & Patrimoine : tout établissement a des équipements
        ["15"] = ModulePack.Core,            // Qualité, Audit & Contrôle interne
        ["16"] = ModulePack.Core,            // Juridique & Conformité
        ["17"] = ModulePack.Core,            // GED / Gestion documentaire
        ["18"] = ModulePack.Hospitality,     // PortMaster / Marina
        ["19"] = ModulePack.Core,            // Parking & Contrôle d'accès
        ["20"] = ModulePack.Pilotage,        // Pilotage, KPI & BI
        ["21"] = ModulePack.System,          // Intégrations & Matériels
        ["22"] = ModulePack.System           // Administration Système
    }.ToFrozenDictionary(StringComparer.Ordinal);

    // Exceptions par entrée historique : quand une entrée n'est pas du paquet de son domaine.
    // Chaque ligne se justifie ; une entrée absente d'ici suit son domaine.
    private static readonly FrozenDictionary<string, ModulePack> PackOverrideByLegacyOrder = new Dictionary<string, ModulePack>(StringComparer.Ordinal)
    {
        // « Conformité hôtelière » (23) vit sous Juridique & Conformité, domaine du socle, mais
        // n'a rien à dire à un commerce : classement, fiches de police, taxe de séjour.
        ["23"] = ModulePack.Hospitality
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, ModulePack> PackByLegacyOrder = BuildPackByLegacyOrder();

    // Paquets actifs par défaut selon le secteur. Le socle, le pilotage et le système sont
    // ajoutés d'office ; ce dictionnaire ne liste que ce qui se choisit.
    private static readonly FrozenDictionary<BusinessSector, IReadOnlySet<ModulePack>> ActivePacksBySector =
        new Dictionary<BusinessSector, IReadOnlySet<ModulePack>>
        {
            [BusinessSector.Hospitality] = Compose(
                ModulePack.Finance, ModulePack.Sales, ModulePack.Purchasing, ModulePack.Inventory, ModulePack.HumanResources,
                ModulePack.Crm, ModulePack.Hospitality, ModulePack.FoodBeverage, ModulePack.Events),
            [BusinessSector.Retail] = Compose(
                ModulePack.Finance, ModulePack.Sales, ModulePack.Purchasing, ModulePack.Inventory, ModulePack.Crm),
            [BusinessSector.Services] = Compose(
                ModulePack.Finance, ModulePack.Sales, ModulePack.Purchasing, ModulePack.HumanResources, ModulePack.Crm),
            [BusinessSector.Manufacturing] = Compose(
                ModulePack.Finance, ModulePack.Sales, ModulePack.Purchasing, ModulePack.Inventory, ModulePack.HumanResources, ModulePack.Crm),
            [BusinessSector.Education] = Compose(
                ModulePack.Finance, ModulePack.Sales, ModulePack.Purchasing, ModulePack.HumanResources, ModulePack.Crm),
            [BusinessSector.Health] = Compose(
                ModulePack.Finance, ModulePack.Sales, ModulePack.Purchasing, ModulePack.Inventory, ModulePack.HumanResources, ModulePack.Crm),
            [BusinessSector.Other] = Compose(
                ModulePack.Finance, ModulePack.Sales, ModulePack.Purchasing, ModulePack.Inventory, ModulePack.HumanResources, ModulePack.Crm)
        }.ToFrozenDictionary();

    static ModulePackCatalog()
    {
        // Le manifeste et la taxonomie fonctionnelle sont deux vues d'une même définition : un
        // domaine ou une entrée sans paquet - ou un paquet cité pour un domaine qui n'existe
        // pas - est une incohérence d'édition, connue dès le chargement du type.
        var domainIds = FunctionalArchitectureCatalog.Domains.Select(domain => domain.Id).ToHashSet(StringComparer.Ordinal);

        if (!domainIds.SetEquals(PackByDomain.Keys))
        {
            throw new InvalidOperationException(
                "Le manifeste des paquets doit rattacher exactement les domaines de FunctionalArchitectureCatalog : "
                + string.Join(", ", domainIds.Except(PackByDomain.Keys).Concat(PackByDomain.Keys.Except(domainIds))));
        }

        if (PackByLegacyOrder.Count != FunctionalArchitectureCatalog.ExpectedLegacyModuleCount)
        {
            throw new InvalidOperationException(
                $"Le manifeste doit rattacher les {FunctionalArchitectureCatalog.ExpectedLegacyModuleCount} entrées historiques, une fois chacune.");
        }

        if (Definitions.Select(definition => definition.Pack).Distinct().Count() != Enum.GetValues<ModulePack>().Length
            || Definitions.Count != Enum.GetValues<ModulePack>().Length)
        {
            throw new InvalidOperationException("Chaque paquet doit avoir exactement une fiche dans le manifeste.");
        }

        if (Enum.GetValues<BusinessSector>().Any(sector => !ActivePacksBySector.ContainsKey(sector)))
        {
            throw new InvalidOperationException("Chaque secteur d'activité doit déclarer ses paquets actifs par défaut.");
        }
    }

    /// <summary>Le paquet d'un domaine fonctionnel (identifiant « 01 » à « 22 »).</summary>
    public static ModulePack PackForDomain(string domainId) =>
        PackByDomain.TryGetValue(domainId, out var pack)
            ? pack
            : throw new KeyNotFoundException($"Le domaine '{domainId}' n'est rattaché à aucun paquet.");

    /// <summary>Le paquet d'une entrée du catalogue historique (« 4.5 », « 22.8 »).</summary>
    public static ModulePack PackForLegacyOrder(string legacyOrder) =>
        PackByLegacyOrder.TryGetValue(legacyOrder, out var pack)
            ? pack
            : throw new KeyNotFoundException($"L'entrée historique '{legacyOrder}' n'est rattachée à aucun paquet.");

    public static bool TryGetPackForLegacyOrder(string legacyOrder, out ModulePack pack) =>
        PackByLegacyOrder.TryGetValue(legacyOrder, out pack);

    /// <summary>
    /// Les paquets actifs par défaut pour un secteur d'activité : le socle, le pilotage et le
    /// système toujours, le reste selon le métier. L'hôtellerie a tout ; un commerce n'a ni PMS,
    /// ni cuisine, ni événementiel. C'est un défaut d'installation, pas une licence : l'hôte
    /// reste libre d'activer un paquet de plus.
    /// </summary>
    public static IReadOnlySet<ModulePack> ActivePacksFor(BusinessSector sector) =>
        ActivePacksBySector.TryGetValue(sector, out var packs)
            ? packs
            : throw new ArgumentOutOfRangeException(nameof(sector), sector, "Secteur d'activité inconnu.");

    /// <summary>La chaîne de licence d'un paquet, à poser sur <see cref="INavigationNode.LicenseFeature"/>.</summary>
    public static string LicenseFeatureFor(ModulePack pack) =>
        LicenseFeaturePrefix + pack.ToString().ToLowerInvariant();

    /// <summary>La chaîne de licence du paquet d'un domaine - ce que le nœud de domaine doit porter.</summary>
    public static string LicenseFeatureForDomain(string domainId) =>
        LicenseFeatureFor(PackForDomain(domainId));

    /// <summary>Lit une chaîne de licence produite par ce manifeste. Faux pour nul, vide ou toute autre notion de licence.</summary>
    public static bool TryParseLicenseFeature(string? licenseFeature, out ModulePack pack)
    {
        pack = default;

        if (licenseFeature is null
            || !licenseFeature.StartsWith(LicenseFeaturePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Enum.TryParse(licenseFeature[LicenseFeaturePrefix.Length..], ignoreCase: true, out pack)
            && Enum.IsDefined(pack);
    }

    /// <summary>
    /// Le point d'extension <see cref="NavigationFilter.LicenseAllows"/> pour un jeu de paquets
    /// actifs. Un nœud sans chaîne de licence passe (rien n'est exigé) ; un nœud qui exige un
    /// paquet passe si ce paquet est actif ; une chaîne d'une autre nature - une future
    /// licence par fonctionnalité - n'est pas de son ressort et passe aussi, pour rester
    /// composable avec le filtre qui la comprendra.
    /// </summary>
    public static Func<string?, bool> LicenseFilter(IReadOnlySet<ModulePack> activePacks)
    {
        ArgumentNullException.ThrowIfNull(activePacks);

        return licenseFeature =>
            !TryParseLicenseFeature(licenseFeature, out var pack) || activePacks.Contains(pack);
    }

    private static FrozenDictionary<string, ModulePack> BuildPackByLegacyOrder()
    {
        var index = new Dictionary<string, ModulePack>(StringComparer.Ordinal);

        foreach (var domain in FunctionalArchitectureCatalog.Domains)
        {
            foreach (var order in domain.LegacyModuleOrders)
            {
                index[order] = PackOverrideByLegacyOrder.TryGetValue(order, out var overridden)
                    ? overridden
                    : PackByDomain[domain.Id];
            }
        }

        // Une exception qui ne désigne aucune entrée connue est une faute de frappe, pas une règle.
        var unknownOverrides = PackOverrideByLegacyOrder.Keys.Where(order => !index.ContainsKey(order)).ToArray();

        if (unknownOverrides.Length > 0)
        {
            throw new InvalidOperationException(
                "Exceptions de paquet sur des entrées historiques inconnues : " + string.Join(", ", unknownOverrides));
        }

        return index.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static IReadOnlySet<ModulePack> Compose(params ModulePack[] optionalPacks) =>
        AlwaysActivePacks.Concat(optionalPacks).ToFrozenSet();
}
