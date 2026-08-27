namespace RaqmiSystem.Domain.Identity;

public static class PermissionCatalog
{
    public const string UsersRead = "users.read";
    public const string UsersWrite = "users.write";
    public const string RolesRead = "roles.read";
    public const string RolesWrite = "roles.write";
    public const string UnitsRead = "units.read";
    public const string UnitsWrite = "units.write";
    public const string RevenueRead = "revenue.read";
    public const string RevenueWrite = "revenue.write";
    public const string RevenueValidate = "revenue.validate";
    public const string DashboardRead = "dashboard.read";
    public const string TreasuryRead = "treasury.read";
    public const string TreasuryWrite = "treasury.write";
    public const string AuditRead = "audit.read";
    public const string ReportsExport = "reports.export";
    public const string SecuritySeed = "security.seed";

    public static IReadOnlyCollection<PermissionDefinition> All { get; } = new[]
    {
        new PermissionDefinition(UsersRead, "Lire les utilisateurs", "security", "Consulter les utilisateurs et profils."),
        new PermissionDefinition(UsersWrite, "Gerer les utilisateurs", "security", "Creer, modifier, activer ou desactiver les utilisateurs."),
        new PermissionDefinition(RolesRead, "Lire les roles", "security", "Consulter les roles et permissions."),
        new PermissionDefinition(RolesWrite, "Gerer les roles", "security", "Modifier les roles et leurs permissions."),
        new PermissionDefinition(UnitsRead, "Lire les unites", "organization", "Consulter les hotels et unites."),
        new PermissionDefinition(UnitsWrite, "Gerer les unites", "organization", "Creer et modifier les hotels et unites."),
        new PermissionDefinition(RevenueRead, "Lire les recettes", "exploitation", "Consulter les recettes journalieres."),
        new PermissionDefinition(RevenueWrite, "Saisir les recettes", "exploitation", "Saisir ou corriger les recettes journalieres."),
        new PermissionDefinition(RevenueValidate, "Valider les recettes", "exploitation", "Valider les recettes apres controle."),
        new PermissionDefinition(DashboardRead, "Lire les tableaux de bord", "reporting", "Consulter les indicateurs de direction."),
        new PermissionDefinition(TreasuryRead, "Lire la tresorerie", "finance", "Consulter caisse, encaissements et mouvements."),
        new PermissionDefinition(TreasuryWrite, "Gerer la tresorerie", "finance", "Creer ou modifier les mouvements de tresorerie."),
        new PermissionDefinition(AuditRead, "Lire l'audit", "security", "Consulter le journal des actions sensibles."),
        new PermissionDefinition(ReportsExport, "Exporter les rapports", "reporting", "Exporter ou imprimer les etats."),
        new PermissionDefinition(SecuritySeed, "Initialiser la securite", "security", "Executer les operations de socle securite.")
    };
}
