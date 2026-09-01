namespace RaqmiSystem.Application.Navigation;

public enum FunctionalMaturity
{
    Planned,
    TechnicalPreview,
    Functional,
    ProductionReady
}

public sealed record FunctionalDomainDefinition(
    string Id,
    string Name,
    string IconKey,
    FunctionalMaturity Maturity,
    IReadOnlyList<string> LegacyModuleOrders);

/// <summary>
/// Taxonomie fonctionnelle stable de Raqmi System. Elle ne dépend ni de WPF ni des TabIndex :
/// tous les clients peuvent donc partager les mêmes identifiants de domaine.
/// </summary>
public static class FunctionalArchitectureCatalog
{
    public const int ExpectedDomainCount = 22;
    public const int ExpectedLegacyModuleCount = 50;

    public static IReadOnlyList<FunctionalDomainDefinition> Domains { get; } =
    [
        Domain("01", "Mon Espace", "Pilotage", FunctionalMaturity.Planned, "22.2", "25.2"),
        Domain("02", "Administration & Socle ERP", "Socle", FunctionalMaturity.Functional, "1", "2", "3"),
        Domain("03", "Finance & Comptabilité", "Finance", FunctionalMaturity.Functional, "4", "5", "5.2", "5.4", "6", "9"),
        Domain("04", "Commercial, Clients & CRM", "Juridique", FunctionalMaturity.Functional, "9.2", "10.4", "18", "20.2"),
        Domain("05", "Facturation & Ventes", "Finance", FunctionalMaturity.Functional, "8"),
        Domain("06", "PMS / Hébergement", "Exploitation", FunctionalMaturity.Functional, "4.5", "10", "10.1"),
        Domain("07", "Revenue Management & Distribution", "Exploitation", FunctionalMaturity.TechnicalPreview, "14.5"),
        Domain("08", "Housekeeping", "Exploitation", FunctionalMaturity.Functional, "10.2"),
        Domain("09", "Groupes, MICE & Événementiel", "Exploitation", FunctionalMaturity.Functional, "10.6"),
        Domain("10", "F&B / Restauration", "Exploitation", FunctionalMaturity.TechnicalPreview, "11.5", "11.6"),
        Domain("11", "Stocks & Économat", "Achats", FunctionalMaturity.Functional, "11"),
        Domain("12", "Achats & Fournisseurs", "Achats", FunctionalMaturity.TechnicalPreview, "12", "12.5"),
        Domain("13", "Ressources Humaines & Paie", "RessourcesHumaines", FunctionalMaturity.Functional, "21"),
        Domain("14", "Maintenance & Patrimoine", "Exploitation", FunctionalMaturity.Planned, "13", "23.4"),
        Domain("15", "Qualité, Audit & Contrôle interne", "Controle", FunctionalMaturity.TechnicalPreview, "22", "22.4", "22.6", "22.8"),
        Domain("16", "Juridique & Conformité", "Conformite", FunctionalMaturity.Planned, "20", "23", "23.2", "23.6"),
        Domain("17", "GED / Gestion documentaire", "Documentaire", FunctionalMaturity.Planned, "27"),
        Domain("18", "PortMaster / Marina", "Specifique", FunctionalMaturity.Planned, "26"),
        Domain("19", "Parking & Contrôle d'accès", "Specifique", FunctionalMaturity.Planned),
        Domain("20", "Pilotage, KPI & BI", "Pilotage", FunctionalMaturity.Functional, "24", "24.2", "24.4", "25", "25.4"),
        Domain("21", "Intégrations & Matériels", "Systeme", FunctionalMaturity.TechnicalPreview, "13.5", "21.2"),
        Domain("22", "Administration Système", "Systeme", FunctionalMaturity.Functional, "28", "29", "30")
    ];

    private static readonly IReadOnlyDictionary<string, FunctionalDomainDefinition> ByLegacyOrder =
        Domains
            .SelectMany(domain => domain.LegacyModuleOrders.Select(order => (order, domain)))
            .ToDictionary(item => item.order, item => item.domain, StringComparer.Ordinal);

    static FunctionalArchitectureCatalog()
    {
        if (Domains.Count != ExpectedDomainCount)
        {
            throw new InvalidOperationException($"Le catalogue doit contenir {ExpectedDomainCount} domaines.");
        }

        if (ByLegacyOrder.Count != ExpectedLegacyModuleCount)
        {
            throw new InvalidOperationException($"Le mapping doit couvrir {ExpectedLegacyModuleCount} modules historiques.");
        }
    }

    public static FunctionalDomainDefinition DomainForLegacyOrder(string legacyOrder) =>
        ByLegacyOrder.TryGetValue(legacyOrder, out var domain)
            ? domain
            : throw new KeyNotFoundException($"Le module historique '{legacyOrder}' n'a aucun domaine cible.");

    public static bool TryGetDomainForLegacyOrder(string legacyOrder, out FunctionalDomainDefinition? domain) =>
        ByLegacyOrder.TryGetValue(legacyOrder, out domain);

    private static FunctionalDomainDefinition Domain(
        string id,
        string name,
        string iconKey,
        FunctionalMaturity maturity,
        params string[] legacyModuleOrders) =>
        new(id, name, iconKey, maturity, legacyModuleOrders);
}
