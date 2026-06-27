namespace Raqmi.Shared;

public enum ModuleFamily
{
    Core,
    Finance,
    Hr,
    Operations,
    Specific,
    System,
}

public sealed record RaqmiModuleDefinition(
    RaqmiModuleCode Code,
    string Label,
    ModuleFamily Family,
    bool Commercial,
    string Description);

public static class ModuleCatalog
{
    public static IReadOnlyList<RaqmiModuleDefinition> All { get; } =
    [
        new(RaqmiModuleCode.Administration, "Administration & utilisateurs", ModuleFamily.Core, false, "Utilisateurs, rôles et permissions."),
        new(RaqmiModuleCode.Settings, "Paramétrage global", ModuleFamily.Core, false, "Paramètres entreprise, devise, délais, branding."),
        new(RaqmiModuleCode.Sites, "Sites / unités", ModuleFamily.Core, true, "Gestion multi-sites, hôtels, unités, agences ou structures."),
        new(RaqmiModuleCode.DailyRevenue, "Recettes journalières", ModuleFamily.Finance, true, "Saisie et validation du chiffre d'affaires quotidien."),
        new(RaqmiModuleCode.Treasury, "Trésorerie", ModuleFamily.Finance, true, "Encaissements, caisse, banques et rapprochements."),
        new(RaqmiModuleCode.Billing, "Facturation", ModuleFamily.Finance, true, "Factures, avoirs, paiements et exports."),
        new(RaqmiModuleCode.Receivables, "Créances & recouvrement", ModuleFamily.Finance, true, "Balance âgée, relances, litiges et recouvrement."),
        new(RaqmiModuleCode.Contracts, "Contrats & conventions", ModuleFamily.Finance, true, "Contrats clients, conventions, tarifs contractuels."),
        new(RaqmiModuleCode.Hr, "Ressources humaines", ModuleFamily.Hr, true, "Employés, contrats, affectations, absences, pointage."),
        new(RaqmiModuleCode.Payroll, "Paie", ModuleFamily.Hr, true, "Préparation paie, variables, retenues et états."),
        new(RaqmiModuleCode.Stocks, "Stocks", ModuleFamily.Operations, true, "Produits, mouvements, seuils et inventaires."),
        new(RaqmiModuleCode.Purchases, "Achats", ModuleFamily.Operations, true, "Fournisseurs, demandes, bons de commande, réception."),
        new(RaqmiModuleCode.Maintenance, "Maintenance", ModuleFamily.Operations, true, "Interventions, équipements et maintenance préventive."),
        new(RaqmiModuleCode.Ged, "Gestion documentaire", ModuleFamily.Operations, true, "Documents, pièces jointes et archivage."),
        new(RaqmiModuleCode.Parking, "Parking", ModuleFamily.Specific, true, "Tickets, abonnements, encaissements parking."),
        new(RaqmiModuleCode.BeachPool, "Plage & piscine", ModuleFamily.Specific, true, "Accès, ventes, équipements et suivi exploitation."),
        new(RaqmiModuleCode.Portmaster, "PortMaster", ModuleFamily.Specific, true, "Port de plaisance, bateaux, contrats, amarrages."),
        new(RaqmiModuleCode.Quality, "Qualité & réclamations", ModuleFamily.Operations, true, "Réclamations, actions correctives, indicateurs qualité."),
        new(RaqmiModuleCode.Checklists, "Checklists de contrôle", ModuleFamily.Operations, true, "Contrôles terrain, preuves, plans d'action."),
        new(RaqmiModuleCode.Reports, "Rapports & exports", ModuleFamily.System, true, "Rapports PDF/Excel, états standards et exports."),
        new(RaqmiModuleCode.Dashboards, "Dashboards directionnels", ModuleFamily.System, true, "KPI, tableaux de bord et pilotage directionnel."),
        new(RaqmiModuleCode.Sync, "Synchronisation", ModuleFamily.System, true, "Synchronisation multi-postes ou cloud."),
        new(RaqmiModuleCode.CloudStorage, "Stockage cloud", ModuleFamily.System, true, "Upload cloud des fichiers, sauvegardes et archives."),
    ];
}
