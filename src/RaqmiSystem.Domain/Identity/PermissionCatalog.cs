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
    public const string ClosingRead = "closing.read";
    public const string ClosingClose = "closing.close";
    public const string ClosingReopen = "closing.reopen";
    public const string TreasuryApprove = "treasury.approve";
    public const string CustomersRead = "customers.read";
    public const string CustomersWrite = "customers.write";
    public const string InvoicesRead = "invoices.read";
    public const string InvoicesWrite = "invoices.write";
    public const string InvoicesIssue = "invoices.issue";
    public const string SettingsRead = "settings.read";
    public const string SettingsWrite = "settings.write";
    public const string AccountingRead = "accounting.read";
    public const string AccountingWrite = "accounting.write";
    public const string AccountingPost = "accounting.post";
    public const string BudgetRead = "budget.read";
    public const string BudgetWrite = "budget.write";
    public const string BudgetApprove = "budget.approve";
    public const string ReceivablesRead = "receivables.read";
    public const string ReceivablesWrite = "receivables.write";
    public const string TariffsRead = "tariffs.read";
    public const string TariffsWrite = "tariffs.write";
    public const string LodgingRead = "lodging.read";
    public const string LodgingWrite = "lodging.write";
    public const string LodgingCheckin = "lodging.checkin";
    public const string ApprovalsRead = "approvals.read";
    public const string ApprovalsWrite = "approvals.write";
    public const string ApprovalsDecide = "approvals.decide";
    public const string ReportsRead = "reports.read";
    public const string MaintenanceRead = "maintenance.read";
    public const string MaintenanceBackup = "maintenance.backup";

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
        new PermissionDefinition(SecuritySeed, "Initialiser la securite", "security", "Executer les operations de socle securite."),
        new PermissionDefinition(ClosingRead, "Lire les clotures", "exploitation", "Consulter les clotures journalieres des unites."),
        new PermissionDefinition(ClosingClose, "Cloturer la journee", "exploitation", "Cloturer officiellement la journee d'exploitation d'une unite."),
        new PermissionDefinition(ClosingReopen, "Reouvrir la journee", "exploitation", "Reouvrir une journee cloturee avec motif obligatoire."),
        new PermissionDefinition(TreasuryApprove, "Approuver les ordres de paiement", "finance", "Approuver les ordres de paiement avant reglement."),
        new PermissionDefinition(CustomersRead, "Lire les clients", "finance", "Consulter le fichier clients."),
        new PermissionDefinition(CustomersWrite, "Gerer les clients", "finance", "Creer, modifier, activer ou desactiver les clients."),
        new PermissionDefinition(InvoicesRead, "Lire les factures", "finance", "Consulter les factures de vente."),
        new PermissionDefinition(InvoicesWrite, "Gerer les factures", "finance", "Creer les brouillons de facture, modifier les lignes, encaisser et annuler."),
        new PermissionDefinition(InvoicesIssue, "Emettre les factures", "finance", "Emettre une facture et allouer son numero definitif."),
        new PermissionDefinition(SettingsRead, "Lire le parametrage global", "configuration", "Consulter l'identite de l'etablissement et les parametres d'exploitation."),
        new PermissionDefinition(SettingsWrite, "Gerer le parametrage global", "configuration", "Modifier l'identite de l'etablissement et les parametres d'exploitation."),
        new PermissionDefinition(AccountingRead, "Lire la comptabilite", "finance", "Consulter le plan comptable, les journaux, les ecritures et la balance."),
        new PermissionDefinition(AccountingWrite, "Saisir la comptabilite", "finance", "Creer et modifier le plan comptable, les journaux et les ecritures en brouillon."),
        new PermissionDefinition(AccountingPost, "Comptabiliser les ecritures", "finance", "Comptabiliser une ecriture et enregistrer une ecriture d'extourne."),
        new PermissionDefinition(BudgetRead, "Lire les budgets", "finance", "Consulter les budgets annuels et les ecarts budget/realise."),
        new PermissionDefinition(BudgetWrite, "Gerer les budgets", "finance", "Creer et modifier les budgets annuels et leurs objectifs mensuels."),
        new PermissionDefinition(BudgetApprove, "Approuver les budgets", "finance", "Approuver et cloturer un budget annuel."),
        new PermissionDefinition(ReceivablesRead, "Lire les creances", "finance", "Consulter la balance agee, les relances et le risque client."),
        new PermissionDefinition(ReceivablesWrite, "Enregistrer les relances", "finance", "Enregistrer la trace d'une relance client deja effectuee."),
        new PermissionDefinition(TariffsRead, "Lire les tarifs", "exploitation", "Consulter les plans tarifaires, les periodes de tarif, les conventions clients et tester la resolution d'un tarif."),
        new PermissionDefinition(TariffsWrite, "Gerer les tarifs", "exploitation", "Creer et modifier les plans tarifaires, definir le plan par defaut, gerer les periodes de tarif et les conventions clients."),
        new PermissionDefinition(LodgingRead, "Lire l'hebergement", "exploitation", "Consulter les types de chambre, les chambres, les reservations, les folios et l'occupation."),
        new PermissionDefinition(LodgingWrite, "Gerer l'hebergement", "exploitation", "Gerer les types de chambre et les chambres, creer, annuler et constater le no-show des reservations."),
        new PermissionDefinition(LodgingCheckin, "Operer le comptoir", "exploitation", "Effectuer les operations du comptoir : check-in, check-out et ajout de lignes de folio."),
        new PermissionDefinition(ApprovalsRead, "Lire les validations", "exploitation", "Consulter les circuits de validation et les demandes d'approbation."),
        new PermissionDefinition(ApprovalsWrite, "Gerer les circuits de validation", "exploitation", "Creer et modifier les circuits de validation, les activer ou les desactiver, et ouvrir une demande."),
        new PermissionDefinition(ApprovalsDecide, "Decider des validations", "exploitation", "Approuver ou rejeter une etape de validation qui vous est assignee."),
        new PermissionDefinition(ReportsRead, "Executer les rapports", "reporting", "Consulter le catalogue des rapports, les executer et consulter le journal d'execution."),
        new PermissionDefinition(MaintenanceRead, "Lire la maintenance", "security", "Consulter l'etat des sauvegardes et la politique de retention."),
        new PermissionDefinition(MaintenanceBackup, "Declencher une sauvegarde", "security", "Declencher manuellement une sauvegarde de la base de donnees.")
    };
}
