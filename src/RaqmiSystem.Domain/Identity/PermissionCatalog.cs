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
    public const string AccountingReconcile = "accounting.reconcile";
    public const string AccountingClose = "accounting.close";
    public const string AccountingReverse = "accounting.reverse";
    public const string AccountingAdmin = "accounting.admin";
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

    // ----------------------------- Module 10 : PMS, cles fines -----------------------------
    // Les trois cles ci-dessus restent en place et gardent leur sens : elles portent le
    // parametrage general et le comptoir. Les cles ci-dessous decoupent les gestes qui ENGAGENT
    // - la vente, le prix, la survente, la levee d'une fermeture, la cloture de la journee - pour
    // qu'ils ne soient pas donnes en bloc a quiconque tient la reception.
    public const string LodgingReserve = "lodging.reserve";
    public const string LodgingCheckout = "lodging.checkout";
    public const string LodgingChangeRate = "lodging.change_rate";
    public const string LodgingRoomMove = "lodging.room_move";
    public const string LodgingOverrideRestriction = "lodging.override_restriction";
    public const string LodgingOverbooking = "lodging.overbooking";
    public const string LodgingNoShow = "lodging.noshow";
    public const string LodgingCancel = "lodging.cancel";
    public const string LodgingManageRooms = "lodging.manage_rooms";
    public const string LodgingManageRates = "lodging.manage_rates";
    public const string LodgingNightAudit = "lodging.night_audit";
    public const string HousekeepingRead = "housekeeping.read";
    public const string HousekeepingWrite = "housekeeping.write";
    public const string HousekeepingInspect = "housekeeping.inspect";
    public const string CrmRead = "crm.read";
    public const string CrmWrite = "crm.write";
    public const string CrmLoyalty = "crm.loyalty";
    public const string ApprovalsRead = "approvals.read";
    public const string ApprovalsWrite = "approvals.write";
    public const string ApprovalsDecide = "approvals.decide";
    public const string ReportsRead = "reports.read";
    public const string MaintenanceRead = "maintenance.read";
    public const string MaintenanceBackup = "maintenance.backup";
    public const string SyncRead = "sync.read";
    public const string MiceRead = "mice.read";
    public const string MiceWrite = "mice.write";
    public const string HrRead = "hr.read";
    public const string HrWrite = "hr.write";
    public const string HrPayroll = "hr.payroll";
    public const string HrPayrollClose = "hr.payroll.close";
    public const string InventoryRead = "inventory.read";
    public const string InventoryWrite = "inventory.write";
    public const string InventoryValidate = "inventory.validate";
    public const string PurchasingRead = "purchasing.read";
    public const string PurchasingWrite = "purchasing.write";
    public const string PurchasingApprove = "purchasing.approve";
    public const string PurchasingReceive = "purchasing.receive";
    public const string KitchenRead = "kitchen.read";
    public const string KitchenWrite = "kitchen.write";

    /// <summary>
    /// Parametrer la bibliotheque KPI. La LECTURE des indicateurs ne cree AUCUNE cle nouvelle :
    /// elle exige <see cref="DashboardRead"/> pour entrer dans le module, puis les cles des
    /// modules sources de chaque indicateur (voir <c>KpiCatalog</c>) - un ratio ne doit jamais
    /// servir de chemin detourne vers une donnee que l'utilisateur n'a pas le droit de lire.
    /// Seule l'ECRITURE en a besoin, parce qu'elle recouvre trois actes que personne ne pouvait
    /// poser jusqu'ici : fixer les bornes d'alerte, rattacher les comptes du plan comptable aux
    /// groupes de gestion qui construisent le GOP, et cloturer un instantane - ce dernier etant
    /// irreversible par construction.
    /// </summary>
    public const string KpiAdmin = "kpi.admin";

    // ============================ Modele cible domaine.ressource.action ============================
    // Lot 2.1 de la reorganisation fonctionnelle. Chaque constante ci-dessous est une cle CIBLE
    // decrite dans PermissionRegistry (domaine, ressource, action, cles historiques couvertes).
    // Les 83 constantes historiques ci-dessus restent intactes et valides : une cle historique
    // vaut les cles cibles qu'elle couvre, au moins une version durant (voir docs/security.md).
    // Le client WPF et le garde de readiness referencent les constantes historiques par nom ;
    // rien ici ne les remplace, tout s'y ajoute.

    // 02 Administration & Socle ERP
    public const string AdminUserRead = "admin.user.read";
    public const string AdminUserCreate = "admin.user.create";
    public const string AdminUserUpdate = "admin.user.update";
    public const string AdminUserDeactivate = "admin.user.deactivate";
    public const string AdminRoleRead = "admin.role.read";
    public const string AdminRoleUpdate = "admin.role.update";
    public const string AdminSecuritySeed = "admin.security.seed";
    public const string AdminUnitRead = "admin.unit.read";
    public const string AdminUnitManage = "admin.unit.manage";
    public const string AdminSettingsRead = "admin.settings.read";
    public const string AdminSettingsUpdate = "admin.settings.update";

    // 03 Finance & Comptabilite
    public const string FinanceRevenueRead = "finance.revenue.read";
    public const string FinanceRevenueRecord = "finance.revenue.record";
    public const string FinanceRevenueValidate = "finance.revenue.validate";
    public const string FinanceTreasuryRead = "finance.treasury.read";
    public const string FinanceBankAccountManage = "finance.bank_account.manage";
    public const string FinanceReceiptManage = "finance.receipt.manage";
    public const string FinancePaymentOrderManage = "finance.payment_order.manage";
    public const string FinancePaymentOrderApprove = "finance.payment_order.approve";
    public const string FinanceAccountingRead = "finance.accounting.read";
    public const string FinanceChartManage = "finance.chart.manage";
    public const string FinanceEntryManage = "finance.entry.manage";
    public const string FinancePartyManage = "finance.party.manage";
    public const string FinanceEntryPost = "finance.entry.post";
    public const string FinanceEntryReverse = "finance.entry.reverse";
    public const string FinancePartyReconcile = "finance.party.reconcile";
    public const string FinancePeriodClose = "finance.period.close";
    public const string FinanceAccountingAdmin = "finance.accounting.admin";
    public const string FinanceBudgetRead = "finance.budget.read";
    public const string FinanceBudgetManage = "finance.budget.manage";
    public const string FinanceBudgetApprove = "finance.budget.approve";
    public const string FinanceReceivableRead = "finance.receivable.read";
    public const string FinanceReceivableRemind = "finance.receivable.remind";

    // 04 Commercial, Clients & CRM
    public const string CrmCustomerRead = "crm.customer.read";
    public const string CrmCustomerManage = "crm.customer.manage";
    public const string CrmGuestRead = "crm.guest.read";
    public const string CrmGuestManage = "crm.guest.manage";
    public const string CrmLoyaltyPost = "crm.loyalty.post";

    // 05 Facturation & Ventes
    public const string BillingInvoiceRead = "billing.invoice.read";
    public const string BillingInvoiceManage = "billing.invoice.manage";
    public const string BillingInvoiceIssue = "billing.invoice.issue";

    // 06 PMS / Hebergement (dont la cloture journaliere)
    public const string LodgingFrontOfficeRead = "lodging.front_office.read";
    public const string LodgingReservationCreate = "lodging.reservation.create";
    public const string LodgingReservationCancel = "lodging.reservation.cancel";
    public const string LodgingReservationNoshow = "lodging.reservation.noshow";
    public const string LodgingRoomManage = "lodging.room.manage";
    public const string LodgingRateManage = "lodging.rate.manage";
    public const string LodgingNightAuditExecute = "lodging.night_audit.execute";
    public const string LodgingCheckinExecute = "lodging.checkin.execute";
    public const string LodgingCheckoutExecute = "lodging.checkout.execute";
    public const string LodgingStayMove = "lodging.stay.move";
    public const string LodgingFolioManage = "lodging.folio.manage";
    public const string LodgingStayChangeRate = "lodging.stay.change_rate";
    public const string LodgingRestrictionOverride = "lodging.restriction.override";
    public const string LodgingReservationOverbook = "lodging.reservation.overbook";
    public const string LodgingClosingRead = "lodging.closing.read";
    public const string LodgingClosingClose = "lodging.closing.close";
    public const string LodgingClosingReopen = "lodging.closing.reopen";

    // 07 Revenue Management & Distribution
    public const string RevenueRateRead = "revenue.rate.read";
    public const string RevenueRateManage = "revenue.rate.manage";

    // 08 Housekeeping
    public const string HousekeepingTaskRead = "housekeeping.task.read";
    public const string HousekeepingTaskManage = "housekeeping.task.manage";
    public const string HousekeepingRoomInspect = "housekeeping.room.inspect";

    // 09 Groupes, MICE & Evenementiel
    public const string MiceEventRead = "mice.event.read";
    public const string MiceEventManage = "mice.event.manage";

    // 10 F&B / Restauration
    public const string FnbKitchenRead = "fnb.kitchen.read";
    public const string FnbKitchenManage = "fnb.kitchen.manage";

    // 11 Stocks & Economat
    public const string InventoryStockRead = "inventory.stock.read";
    public const string InventoryItemManage = "inventory.item.manage";
    public const string InventoryMovementRecord = "inventory.movement.record";
    public const string InventoryCountManage = "inventory.count.manage";
    public const string InventoryCountValidate = "inventory.count.validate";

    // 12 Achats & Fournisseurs
    public const string PurchasingOrderRead = "purchasing.order.read";
    public const string PurchasingSupplierManage = "purchasing.supplier.manage";
    public const string PurchasingOrderManage = "purchasing.order.manage";
    public const string PurchasingOrderApprove = "purchasing.order.approve";
    public const string PurchasingReceiptExecute = "purchasing.receipt.execute";

    // 13 Ressources Humaines & Paie (hr.payroll.close, deja au format cible, est sa propre cible)
    public const string HrEmployeeRead = "hr.employee.read";
    public const string HrEmployeeManage = "hr.employee.manage";
    public const string HrTimeManage = "hr.time.manage";
    public const string HrPayrollProcess = "hr.payroll.process";

    // Workflow (service transversal rendu dans 01 Mon Espace, configure dans 02)
    public const string WorkflowRequestRead = "workflow.request.read";
    public const string WorkflowCircuitManage = "workflow.circuit.manage";
    public const string WorkflowRequestDecide = "workflow.request.decide";

    // 20 Pilotage, KPI & BI
    public const string PilotageDashboardRead = "pilotage.dashboard.read";
    public const string PilotageReportExecute = "pilotage.report.execute";
    public const string PilotageReportExport = "pilotage.report.export";
    public const string PilotageKpiAdmin = "pilotage.kpi.admin";

    // Audit (service transversal) et 22 Administration Systeme
    public const string AuditLogRead = "audit.log.read";
    public const string SystemBackupRead = "system.backup.read";
    public const string SystemBackupExecute = "system.backup.execute";
    public const string SystemWorkstationRead = "system.workstation.read";

    /// <summary>
    /// Toutes les permissions seedees et dotees d'une politique : les 83 definitions historiques,
    /// puis les cles cibles du registre qui n'y figurent pas deja (hr.payroll.close est dans les
    /// deux, une seule fois ici). L'ordre est stable : historique d'abord, registre ensuite.
    /// </summary>
    public static IReadOnlyCollection<PermissionDefinition> All => AllDefinitions;

    /// <summary>Les 83 definitions historiques, dans leur ordre d'origine.</summary>
    public static IReadOnlyCollection<PermissionDefinition> Legacy { get; } = new[]
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
        new PermissionDefinition(AccountingReconcile, "Lettrer les tiers", "finance", "Effectuer un lettrage partiel ou total."),
        new PermissionDefinition(AccountingClose, "Cloturer la comptabilite", "finance", "Cloturer les periodes et exercices comptables."),
        new PermissionDefinition(AccountingReverse, "Contrepasser les ecritures", "finance", "Enregistrer une contre-passation liee a l'ecriture source."),
        new PermissionDefinition(AccountingAdmin, "Administrer la comptabilite", "finance", "Initialiser le SCF et administrer les referentiels comptables."),
        new PermissionDefinition(BudgetRead, "Lire les budgets", "finance", "Consulter les budgets annuels et les ecarts budget/realise."),
        new PermissionDefinition(BudgetWrite, "Gerer les budgets", "finance", "Creer et modifier les budgets annuels et leurs objectifs mensuels."),
        new PermissionDefinition(BudgetApprove, "Approuver les budgets", "finance", "Approuver et cloturer un budget annuel."),
        new PermissionDefinition(ReceivablesRead, "Lire les creances", "finance", "Consulter la balance agee, les relances et le risque client."),
        new PermissionDefinition(ReceivablesWrite, "Enregistrer les relances", "finance", "Enregistrer la trace d'une relance client deja effectuee."),
        new PermissionDefinition(TariffsRead, "Lire les tarifs", "exploitation", "Consulter les plans tarifaires, les periodes de tarif, les conventions clients et tester la resolution d'un tarif."),
        new PermissionDefinition(TariffsWrite, "Gerer les tarifs", "exploitation", "Creer et modifier les plans tarifaires, definir le plan par defaut, gerer les periodes de tarif et les conventions clients."),
        new PermissionDefinition(LodgingRead, "Lire l'hebergement", "exploitation", "Consulter les types de chambre, les chambres, les reservations, les folios et l'occupation."),
        new PermissionDefinition(LodgingWrite, "Gerer l'hebergement", "exploitation", "Gerer les types de chambre et les chambres, creer, annuler et constater le no-show des reservations."),
        new PermissionDefinition(LodgingCheckin, "Operer le comptoir", "exploitation", "Effectuer les operations du comptoir : enregistrer une arrivee, tenir les folios et les acomptes."),
        new PermissionDefinition(LodgingReserve, "Vendre l'hebergement", "exploitation", "Creer une reservation ou un walk-in, affecter une chambre, prolonger un sejour."),
        new PermissionDefinition(LodgingCheckout, "Enregistrer les departs", "exploitation", "Enregistrer un depart : il exige un solde nul sur tous les folios du sejour."),
        new PermissionDefinition(LodgingChangeRate, "Modifier le tarif d'un sejour", "exploitation", "Surclasser ou declasser un sejour en facturant l'ecart, et faire reposer les tarifs a venir."),
        new PermissionDefinition(LodgingRoomMove, "Changer un client de chambre", "exploitation", "Deplacer un sejour vers une autre chambre, avec motif obligatoire."),
        new PermissionDefinition(LodgingOverrideRestriction, "Passer outre une restriction", "exploitation", "Vendre malgre un stop sell, un CTA, un CTD ou une duree de sejour imposee."),
        new PermissionDefinition(LodgingOverbooking, "Vendre en surreservation", "exploitation", "Vendre au-dela de la capacite physique, dans la limite autorisee pour la periode."),
        new PermissionDefinition(LodgingNoShow, "Constater les no-shows", "exploitation", "Constater une non-presentation et declencher la penalite prevue par la politique figee."),
        new PermissionDefinition(LodgingCancel, "Annuler une reservation", "exploitation", "Annuler un dossier avec motif et appliquer la penalite prevue par la politique figee."),
        new PermissionDefinition(LodgingManageRooms, "Gerer le parc de chambres", "exploitation", "Creer et modifier les types et les chambres, et poser les blocages hors service (OOO/OOS)."),
        new PermissionDefinition(LodgingManageRates, "Gerer les regles de vente PMS", "exploitation", "Parametrer restrictions, surreservation, extras, forfaits, politiques d'annulation et regles de yield."),
        new PermissionDefinition(LodgingNightAudit, "Passer le night audit", "exploitation", "Executer le night audit d'une journee d'exploitation : controles, posting des nuitees et rapport."),
        new PermissionDefinition(HousekeepingRead, "Lire le housekeeping", "exploitation", "Consulter le tableau des chambres, les taches de nettoyage, le planning des equipes, la carte minibar et les consommations."),
        new PermissionDefinition(HousekeepingWrite, "Gerer le housekeeping", "exploitation", "Planifier et affecter les taches de nettoyage, declarer l'etat des chambres, gerer la carte minibar et enregistrer les consommations."),
        new PermissionDefinition(HousekeepingInspect, "Controler les chambres", "exploitation", "Rendre le verdict de controle sur une chambre nettoyee : accepter la chambre ou la refuser avec motif."),
        new PermissionDefinition(CrmRead, "Lire le CRM", "exploitation", "Consulter la vue client 360, les segments, le programme de fidelite, les campagnes, la satisfaction (NPS) et le journal des contacts."),
        new PermissionDefinition(CrmWrite, "Gerer la relation client", "exploitation", "Qualifier les clients (segment, preferences, consentement marketing), gerer les segments, les paliers de fidelite et les campagnes, enregistrer les enquetes de satisfaction et les contacts."),
        new PermissionDefinition(CrmLoyalty, "Mouvementer les points de fidelite", "exploitation", "Crediter, debiter, corriger ou faire expirer les points de fidelite d'un client."),
        new PermissionDefinition(ApprovalsRead, "Lire les validations", "exploitation", "Consulter les circuits de validation et les demandes d'approbation."),
        new PermissionDefinition(ApprovalsWrite, "Gerer les circuits de validation", "exploitation", "Creer et modifier les circuits de validation, les activer ou les desactiver, et ouvrir une demande."),
        new PermissionDefinition(ApprovalsDecide, "Decider des validations", "exploitation", "Approuver ou rejeter une etape de validation qui vous est assignee."),
        new PermissionDefinition(ReportsRead, "Executer les rapports", "reporting", "Consulter le catalogue des rapports, les executer et consulter le journal d'execution."),
        new PermissionDefinition(MaintenanceRead, "Lire la maintenance", "security", "Consulter l'etat des sauvegardes et la politique de retention."),
        new PermissionDefinition(MaintenanceBackup, "Declencher une sauvegarde", "security", "Declencher manuellement une sauvegarde de la base de donnees."),
        new PermissionDefinition(SyncRead, "Lire le registre des postes", "security", "Consulter les postes declares, leur dernier contact et les erreurs remontees par les clients."),
        new PermissionDefinition(MiceRead, "Lire l'evenementiel", "exploitation", "Consulter les espaces de reception, les evenements, les devis et les BEO."),
        new PermissionDefinition(MiceWrite, "Gerer l'evenementiel", "exploitation", "Creer et modifier les espaces de reception et les evenements, chiffrer un devis, saisir un BEO, confirmer ou annuler."),
        new PermissionDefinition(HrRead, "Lire les ressources humaines", "rh", "Consulter le referentiel des postes, les dossiers collaborateurs, les contrats, les pointages et les absences."),
        new PermissionDefinition(HrWrite, "Gerer les collaborateurs", "rh", "Creer et modifier les departements, les postes, les dossiers collaborateurs, les contrats, les pointages et les absences."),
        new PermissionDefinition(HrPayroll, "Preparer la paie", "rh", "Parametrer les baremes legaux, saisir les primes, generer la pre-paie et valider les bulletins."),
        new PermissionDefinition(HrPayrollClose, "Cloturer la paie", "rh", "Valider une periode de paie puis la cloturer definitivement, ce qui verrouille le mois."),
        new PermissionDefinition(InventoryRead, "Lire les stocks", "exploitation", "Consulter les magasins, les articles, le registre des mouvements, le stock valorise et les inventaires physiques."),
        new PermissionDefinition(InventoryWrite, "Gerer les stocks", "exploitation", "Creer et modifier magasins et articles, enregistrer entrees, sorties, transferts et ajustements, ouvrir et saisir les inventaires."),
        new PermissionDefinition(InventoryValidate, "Valider les inventaires", "exploitation", "Valider un inventaire physique : generer les mouvements d'ajustement et figer definitivement l'inventaire."),
        new PermissionDefinition(PurchasingRead, "Lire les achats", "exploitation", "Consulter le referentiel fournisseurs, les bons de commande et l'avancement des receptions."),
        new PermissionDefinition(PurchasingWrite, "Gerer les achats", "exploitation", "Creer et modifier les fournisseurs, saisir les bons de commande en brouillon, modifier leurs lignes et annuler une commande avec motif."),
        new PermissionDefinition(PurchasingApprove, "Approuver les bons de commande", "exploitation", "Approuver un bon de commande : allouer son numero definitif et figer ses lignes - l'acte qui engage la depense."),
        new PermissionDefinition(PurchasingReceive, "Receptionner les marchandises", "exploitation", "Enregistrer une reception (totale ou partielle) contre un bon de commande approuve et generer l'entree en stock correspondante."),
        new PermissionDefinition(KitchenRead, "Lire la cuisine", "exploitation", "Consulter les fiches techniques, leur cout matiere, les points de controle HACCP et les releves de temperature."),
        new PermissionDefinition(KitchenWrite, "Gerer la cuisine", "exploitation", "Creer et modifier les fiches techniques et leurs ingredients, administrer les points de controle et leurs seuils, enregistrer les releves de temperature HACCP."),
        new PermissionDefinition(KpiAdmin, "Parametrer la bibliotheque KPI", "reporting", "Fixer les seuils et objectifs des indicateurs, rattacher les comptes du plan comptable aux groupes de gestion du resultat, et cloturer les instantanes historises.")
    };

    // Declare APRES Legacy : les initialiseurs statiques s'executent dans l'ordre du texte, et
    // BuildAll lit Legacy.
    private static readonly PermissionDefinition[] AllDefinitions = BuildAll();

    private static PermissionDefinition[] BuildAll()
    {
        var legacyKeys = Legacy.Select(definition => definition.Key).ToHashSet(StringComparer.Ordinal);

        var targets = PermissionRegistry.All
            .Where(target => !legacyKeys.Contains(target.Key))
            .Select(target => new PermissionDefinition(target.Key, target.Name, target.Prefix, target.Description));

        return Legacy.Concat(targets).ToArray();
    }
}
