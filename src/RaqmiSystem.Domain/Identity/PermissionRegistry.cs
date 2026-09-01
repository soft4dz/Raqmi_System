namespace RaqmiSystem.Domain.Identity;

/// <summary>
/// Registre du modele de permissions cible <c>domaine.ressource.action</c> (lot 2.1 de la
/// reorganisation fonctionnelle) : la table de correspondance entre les 83 cles historiques du
/// catalogue et les cles cibles qui les remplacent, avec pour chacune le domaine fonctionnel qui
/// la porte, sa ressource, son action et sa description.
///
/// La relation entre cle historique et cle cible est volontairement ASYMETRIQUE :
/// <list type="bullet">
///   <item>une cle historique VAUT toutes les cles cibles qu'elle couvre - un profil qui la
///         detient ne perd rien le jour ou une route est retaguee vers la cle fine ;</item>
///   <item>une cle cible ne vaut la cle historique QUE si celle-ci ne couvre qu'elle (mapping
///         1:1, alias equivalent dans les deux sens). Une cle historique qui en couvre plusieurs
///         (users.write, lodging.write, accounting.write...) est COMPOSITE : detenir une seule de
///         ses cles fines ne l'equivaut jamais, sinon la cle fine serait un chemin detourne vers
///         tout ce que la cle historique ouvre encore sur les routes non retaguees.</item>
/// </list>
///
/// Les huit alias PMS que Program.cs declarait un par un (lodging.reserve accepte lodging.write,
/// lodging.checkout accepte lodging.checkin, etc.) sont exprimes ici comme des couvertures
/// ordinaires : la cle fine historique et la cle large historique couvrent toutes deux la meme
/// cle cible, et la politique de la cle fine reste egale a celle de sa cible. Les trois cles
/// fines qui n'avaient volontairement PAS d'alias (change_rate, override_restriction,
/// overbooking) n'en recoivent toujours pas : les faire couvrir par lodging.write reviendrait a
/// accorder retroactivement des gestes qui n'existaient pas quand la cle a ete donnee.
///
/// Les identifiants de domaine sont ceux de <c>FunctionalArchitectureCatalog</c> (couche
/// Application). Ils sont repris ici en constantes plutot qu'importes : le Domain ne depend pas
/// de la couche Navigation, et un identifiant stable n'a pas vocation a changer.
/// </summary>
public static class PermissionRegistry
{
    public const string DomainMonEspace = "01";
    public const string DomainAdministration = "02";
    public const string DomainFinance = "03";
    public const string DomainCrm = "04";
    public const string DomainBilling = "05";
    public const string DomainLodging = "06";
    public const string DomainRevenue = "07";
    public const string DomainHousekeeping = "08";
    public const string DomainMice = "09";
    public const string DomainFnb = "10";
    public const string DomainInventory = "11";
    public const string DomainPurchasing = "12";
    public const string DomainHumanResources = "13";
    public const string DomainPilotage = "20";
    public const string DomainSystem = "22";

    /// <summary>
    /// Conventions d'action, pour que le registre se lise d'une seule voix :
    /// <c>read</c> consulter ; <c>manage</c> creer, modifier, activer ou desactiver un
    /// referentiel ou un document en brouillon (c'est le "write" historique) ; puis un verbe
    /// propre pour chaque acte qui ENGAGE l'etablissement et merite sa propre cle : post, close,
    /// reverse, approve, issue, validate, execute, decide, reconcile, inspect, process, export...
    /// </summary>
    public static IReadOnlyCollection<TargetPermissionDefinition> All { get; } = new[]
    {
        // ------------------------------ 02 Administration & Socle ERP ------------------------------
        Target(PermissionCatalog.AdminUserRead, DomainAdministration, "Lire les utilisateurs",
            "Consulter les comptes utilisateurs et leurs roles.", PermissionCatalog.UsersRead),
        // users.write est le mapping 1:n de reference (livrable 4, section 4.E) : creer, modifier et
        // desactiver un compte sont trois actes distincts, et la cle historique vaut les trois.
        Target(PermissionCatalog.AdminUserCreate, DomainAdministration, "Creer un utilisateur",
            "Creer un compte utilisateur avec un mot de passe temporaire.", PermissionCatalog.UsersWrite),
        Target(PermissionCatalog.AdminUserUpdate, DomainAdministration, "Modifier un utilisateur",
            "Modifier le profil d'un compte, ses roles, le deverrouiller ou reinitialiser son mot de passe.", PermissionCatalog.UsersWrite),
        Target(PermissionCatalog.AdminUserDeactivate, DomainAdministration, "Activer ou desactiver un utilisateur",
            "Activer ou desactiver un compte utilisateur.", PermissionCatalog.UsersWrite),
        Target(PermissionCatalog.AdminRoleRead, DomainAdministration, "Lire les roles",
            "Consulter les roles et leurs permissions.", PermissionCatalog.RolesRead),
        Target(PermissionCatalog.AdminRoleUpdate, DomainAdministration, "Gerer les roles",
            "Modifier les roles et leurs permissions.", PermissionCatalog.RolesWrite),
        Target(PermissionCatalog.AdminSecuritySeed, DomainAdministration, "Initialiser la securite",
            "Executer les operations de socle securite, dont la purge du journal d'audit.", PermissionCatalog.SecuritySeed),
        Target(PermissionCatalog.AdminUnitRead, DomainAdministration, "Lire les unites",
            "Consulter les hotels et unites de l'organisation.", PermissionCatalog.UnitsRead),
        Target(PermissionCatalog.AdminUnitManage, DomainAdministration, "Gerer les unites",
            "Creer et modifier les hotels et unites de l'organisation.", PermissionCatalog.UnitsWrite),
        Target(PermissionCatalog.AdminSettingsRead, DomainAdministration, "Lire le parametrage global",
            "Consulter l'identite de l'etablissement et les parametres d'exploitation.", PermissionCatalog.SettingsRead),
        Target(PermissionCatalog.AdminSettingsUpdate, DomainAdministration, "Gerer le parametrage global",
            "Modifier l'identite de l'etablissement et les parametres d'exploitation.", PermissionCatalog.SettingsWrite),

        // ------------------------------ 03 Finance & Comptabilite ------------------------------
        Target(PermissionCatalog.FinanceRevenueRead, DomainFinance, "Lire les recettes",
            "Consulter les recettes journalieres.", PermissionCatalog.RevenueRead),
        Target(PermissionCatalog.FinanceRevenueRecord, DomainFinance, "Saisir les recettes",
            "Saisir ou corriger les recettes journalieres.", PermissionCatalog.RevenueWrite),
        Target(PermissionCatalog.FinanceRevenueValidate, DomainFinance, "Valider les recettes",
            "Valider les recettes journalieres apres controle.", PermissionCatalog.RevenueValidate),
        Target(PermissionCatalog.FinanceTreasuryRead, DomainFinance, "Lire la tresorerie",
            "Consulter les comptes bancaires, les encaissements, les ordres de paiement et la synthese de tresorerie.", PermissionCatalog.TreasuryRead),
        // treasury.write couvre trois ressources ; l'ordre de paiement est distingue parce que son
        // approbation (treasury.approve) l'etait deja, et que le regler engage la caisse.
        Target(PermissionCatalog.FinanceBankAccountManage, DomainFinance, "Gerer les comptes bancaires",
            "Creer, modifier, activer ou desactiver les comptes bancaires et caisses.", PermissionCatalog.TreasuryWrite),
        Target(PermissionCatalog.FinanceReceiptManage, DomainFinance, "Gerer les encaissements",
            "Saisir, modifier, confirmer ou annuler un encaissement.", PermissionCatalog.TreasuryWrite),
        Target(PermissionCatalog.FinancePaymentOrderManage, DomainFinance, "Gerer les ordres de paiement",
            "Creer un ordre de paiement, le regler une fois approuve, ou l'annuler.", PermissionCatalog.TreasuryWrite),
        Target(PermissionCatalog.FinancePaymentOrderApprove, DomainFinance, "Approuver les ordres de paiement",
            "Approuver un ordre de paiement avant son reglement.", PermissionCatalog.TreasuryApprove),
        Target(PermissionCatalog.FinanceAccountingRead, DomainFinance, "Lire la comptabilite",
            "Consulter le plan comptable, les journaux, les ecritures, les exercices, les tiers, le grand livre et les balances.", PermissionCatalog.AccountingRead),
        // accounting.write couvre le parametrage (plan et journaux), la saisie des ecritures en
        // brouillon et les tiers : trois ressources que la comptabilite cible distingue.
        Target(PermissionCatalog.FinanceChartManage, DomainFinance, "Gerer le plan comptable et les journaux",
            "Creer, modifier, activer ou desactiver les comptes du plan comptable et les journaux.", PermissionCatalog.AccountingWrite),
        Target(PermissionCatalog.FinanceEntryManage, DomainFinance, "Saisir les ecritures",
            "Creer une ecriture en brouillon, modifier ses lignes ou l'annuler avant comptabilisation.", PermissionCatalog.AccountingWrite),
        Target(PermissionCatalog.FinancePartyManage, DomainFinance, "Gerer les tiers comptables",
            "Creer les tiers comptables (clients, fournisseurs, autres).", PermissionCatalog.AccountingWrite),
        Target(PermissionCatalog.FinanceEntryPost, DomainFinance, "Comptabiliser les ecritures",
            "Comptabiliser une ecriture : l'acte qui engage les comptes.", PermissionCatalog.AccountingPost),
        Target(PermissionCatalog.FinanceEntryReverse, DomainFinance, "Contrepasser les ecritures",
            "Enregistrer une contre-passation liee a l'ecriture source.", PermissionCatalog.AccountingReverse),
        Target(PermissionCatalog.FinancePartyReconcile, DomainFinance, "Lettrer les tiers",
            "Effectuer un lettrage partiel ou total sur un compte de tiers.", PermissionCatalog.AccountingReconcile),
        Target(PermissionCatalog.FinancePeriodClose, DomainFinance, "Cloturer les periodes comptables",
            "Cloturer une periode ou un exercice comptable.", PermissionCatalog.AccountingClose),
        Target(PermissionCatalog.FinanceAccountingAdmin, DomainFinance, "Administrer la comptabilite",
            "Initialiser le SCF, ouvrir les exercices et administrer les referentiels comptables.", PermissionCatalog.AccountingAdmin),
        Target(PermissionCatalog.FinanceBudgetRead, DomainFinance, "Lire les budgets",
            "Consulter les budgets annuels et les ecarts budget/realise.", PermissionCatalog.BudgetRead),
        Target(PermissionCatalog.FinanceBudgetManage, DomainFinance, "Gerer les budgets",
            "Creer et modifier les budgets annuels et leurs objectifs mensuels tant qu'ils sont en brouillon.", PermissionCatalog.BudgetWrite),
        Target(PermissionCatalog.FinanceBudgetApprove, DomainFinance, "Approuver les budgets",
            "Approuver puis cloturer un budget annuel, ce qui fige les objectifs.", PermissionCatalog.BudgetApprove),
        Target(PermissionCatalog.FinanceReceivableRead, DomainFinance, "Lire les creances",
            "Consulter la balance agee, les relances et le risque client.", PermissionCatalog.ReceivablesRead),
        Target(PermissionCatalog.FinanceReceivableRemind, DomainFinance, "Enregistrer les relances",
            "Enregistrer la trace d'une relance client deja effectuee.", PermissionCatalog.ReceivablesWrite),

        // ------------------------------ 04 Commercial, Clients & CRM ------------------------------
        Target(PermissionCatalog.CrmCustomerRead, DomainCrm, "Lire les clients",
            "Consulter le fichier clients.", PermissionCatalog.CustomersRead),
        Target(PermissionCatalog.CrmCustomerManage, DomainCrm, "Gerer les clients",
            "Creer, modifier, activer ou desactiver les clients.", PermissionCatalog.CustomersWrite),
        Target(PermissionCatalog.CrmGuestRead, DomainCrm, "Lire le CRM",
            "Consulter la vue client 360, les segments, le programme de fidelite, les campagnes, la satisfaction et le journal des contacts.", PermissionCatalog.CrmRead),
        Target(PermissionCatalog.CrmGuestManage, DomainCrm, "Gerer la relation client",
            "Qualifier les clients, gerer les segments, les paliers de fidelite et les campagnes, enregistrer les enquetes et les contacts.", PermissionCatalog.CrmWrite),
        Target(PermissionCatalog.CrmLoyaltyPost, DomainCrm, "Mouvementer les points de fidelite",
            "Crediter, debiter, corriger ou faire expirer les points de fidelite d'un client.", PermissionCatalog.CrmLoyalty),

        // ------------------------------ 05 Facturation & Ventes ------------------------------
        Target(PermissionCatalog.BillingInvoiceRead, DomainBilling, "Lire les factures",
            "Consulter les factures de vente.", PermissionCatalog.InvoicesRead),
        Target(PermissionCatalog.BillingInvoiceManage, DomainBilling, "Gerer les factures",
            "Creer les brouillons de facture, modifier les lignes, encaisser et annuler.", PermissionCatalog.InvoicesWrite),
        Target(PermissionCatalog.BillingInvoiceIssue, DomainBilling, "Emettre les factures",
            "Emettre une facture et allouer son numero definitif.", PermissionCatalog.InvoicesIssue),

        // ------------------------------ 06 PMS / Hebergement ------------------------------
        Target(PermissionCatalog.LodgingFrontOfficeRead, DomainLodging, "Lire l'hebergement",
            "Consulter les types de chambre, les chambres, les reservations, les folios, l'occupation, le planning et les rapports du front office.", PermissionCatalog.LodgingRead),
        // lodging.write est composite : les six cles fines qu'il couvrait deja par alias (Program.cs)
        // deviennent les cibles ; chaque cle fine historique reste un alias 1:1 de sa cible.
        Target(PermissionCatalog.LodgingReservationCreate, DomainLodging, "Vendre l'hebergement",
            "Creer une reservation ou un walk-in, la modifier, garantir, affecter une chambre, prolonger un sejour et gerer ses extras.", PermissionCatalog.LodgingReserve, PermissionCatalog.LodgingWrite),
        Target(PermissionCatalog.LodgingReservationCancel, DomainLodging, "Annuler une reservation",
            "Annuler un dossier avec motif, appliquer la penalite prevue et conserver un acompte.", PermissionCatalog.LodgingCancel, PermissionCatalog.LodgingWrite),
        Target(PermissionCatalog.LodgingReservationNoshow, DomainLodging, "Constater les no-shows",
            "Constater une non-presentation et declencher la penalite prevue par la politique figee.", PermissionCatalog.LodgingNoShow, PermissionCatalog.LodgingWrite),
        Target(PermissionCatalog.LodgingRoomManage, DomainLodging, "Gerer le parc de chambres",
            "Creer et modifier les types et les chambres, et poser les blocages hors service (OOO/OOS).", PermissionCatalog.LodgingManageRooms, PermissionCatalog.LodgingWrite),
        Target(PermissionCatalog.LodgingRateManage, DomainLodging, "Gerer les regles de vente PMS",
            "Parametrer restrictions, surreservation, extras, forfaits, politiques d'annulation et regles de yield.", PermissionCatalog.LodgingManageRates, PermissionCatalog.LodgingWrite),
        Target(PermissionCatalog.LodgingNightAuditExecute, DomainLodging, "Passer le night audit",
            "Executer le night audit d'une journee d'exploitation : controles, posting des nuitees et rapport.", PermissionCatalog.LodgingNightAudit, PermissionCatalog.LodgingWrite),
        // lodging.checkin ("operer le comptoir") est composite lui aussi : l'arrivee, le depart, le
        // changement de chambre et la tenue des folios sont quatre gestes.
        Target(PermissionCatalog.LodgingCheckinExecute, DomainLodging, "Enregistrer les arrivees",
            "Enregistrer l'arrivee d'un client (check-in).", PermissionCatalog.LodgingCheckin),
        Target(PermissionCatalog.LodgingCheckoutExecute, DomainLodging, "Enregistrer les departs",
            "Preparer et enregistrer un depart : il exige un solde nul sur tous les folios du sejour.", PermissionCatalog.LodgingCheckout, PermissionCatalog.LodgingCheckin),
        Target(PermissionCatalog.LodgingStayMove, DomainLodging, "Changer un client de chambre",
            "Deplacer un sejour vers une autre chambre, avec motif obligatoire.", PermissionCatalog.LodgingRoomMove, PermissionCatalog.LodgingCheckin),
        Target(PermissionCatalog.LodgingFolioManage, DomainLodging, "Tenir les folios",
            "Ouvrir des folios, y porter des charges, transferer, encaisser, imputer ou rembourser des acomptes.", PermissionCatalog.LodgingCheckin),
        Target(PermissionCatalog.LodgingStayChangeRate, DomainLodging, "Modifier le tarif d'un sejour",
            "Surclasser ou declasser un sejour en facturant l'ecart, et faire reposer les tarifs a venir.", PermissionCatalog.LodgingChangeRate),
        Target(PermissionCatalog.LodgingRestrictionOverride, DomainLodging, "Passer outre une restriction",
            "Vendre malgre un stop sell, un CTA, un CTD ou une duree de sejour imposee.", PermissionCatalog.LodgingOverrideRestriction),
        Target(PermissionCatalog.LodgingReservationOverbook, DomainLodging, "Vendre en surreservation",
            "Vendre au-dela de la capacite physique, dans la limite autorisee pour la periode.", PermissionCatalog.LodgingOverbooking),
        // La cloture journaliere est rattachee au PMS (livrable 4, entree 4.5) : c'est la journee
        // d'exploitation qu'elle ferme, pas l'exercice comptable.
        Target(PermissionCatalog.LodgingClosingRead, DomainLodging, "Lire les clotures journalieres",
            "Consulter les clotures journalieres des unites.", PermissionCatalog.ClosingRead),
        Target(PermissionCatalog.LodgingClosingClose, DomainLodging, "Cloturer la journee",
            "Cloturer officiellement la journee d'exploitation d'une unite.", PermissionCatalog.ClosingClose),
        Target(PermissionCatalog.LodgingClosingReopen, DomainLodging, "Reouvrir la journee",
            "Reouvrir une journee cloturee avec motif obligatoire.", PermissionCatalog.ClosingReopen),

        // ------------------------------ 07 Revenue Management & Distribution ------------------------------
        Target(PermissionCatalog.RevenueRateRead, DomainRevenue, "Lire les tarifs",
            "Consulter les plans tarifaires, les periodes de tarif, les conventions clients et tester la resolution d'un tarif.", PermissionCatalog.TariffsRead),
        Target(PermissionCatalog.RevenueRateManage, DomainRevenue, "Gerer les tarifs",
            "Creer et modifier les plans tarifaires, definir le plan par defaut, gerer les periodes de tarif et les conventions clients.", PermissionCatalog.TariffsWrite),

        // ------------------------------ 08 Housekeeping ------------------------------
        Target(PermissionCatalog.HousekeepingTaskRead, DomainHousekeeping, "Lire le housekeeping",
            "Consulter le tableau des chambres, les taches de nettoyage, le planning des equipes, la carte minibar et les consommations.", PermissionCatalog.HousekeepingRead),
        Target(PermissionCatalog.HousekeepingTaskManage, DomainHousekeeping, "Gerer le housekeeping",
            "Planifier et affecter les taches, declarer l'etat des chambres, gerer la carte minibar et enregistrer les consommations.", PermissionCatalog.HousekeepingWrite),
        Target(PermissionCatalog.HousekeepingRoomInspect, DomainHousekeeping, "Controler les chambres",
            "Rendre le verdict de controle sur une chambre nettoyee : l'accepter ou la refuser avec motif.", PermissionCatalog.HousekeepingInspect),

        // ------------------------------ 09 Groupes, MICE & Evenementiel ------------------------------
        Target(PermissionCatalog.MiceEventRead, DomainMice, "Lire l'evenementiel",
            "Consulter les espaces de reception, les evenements, les devis, les BEO et les allotements.", PermissionCatalog.MiceRead),
        Target(PermissionCatalog.MiceEventManage, DomainMice, "Gerer l'evenementiel",
            "Creer et modifier les espaces et les evenements, chiffrer un devis, saisir un BEO, confirmer ou annuler, poser un allotement.", PermissionCatalog.MiceWrite),

        // ------------------------------ 10 F&B / Restauration ------------------------------
        Target(PermissionCatalog.FnbKitchenRead, DomainFnb, "Lire la cuisine",
            "Consulter les fiches techniques, leur cout matiere, les points de controle HACCP et les releves de temperature.", PermissionCatalog.KitchenRead),
        Target(PermissionCatalog.FnbKitchenManage, DomainFnb, "Gerer la cuisine",
            "Creer et modifier les fiches techniques, administrer les points de controle HACCP et enregistrer les releves.", PermissionCatalog.KitchenWrite),

        // ------------------------------ 11 Stocks & Economat ------------------------------
        Target(PermissionCatalog.InventoryStockRead, DomainInventory, "Lire les stocks",
            "Consulter les magasins, les articles, le registre des mouvements, le stock valorise et les inventaires physiques.", PermissionCatalog.InventoryRead),
        // inventory.write couvre le referentiel, les mouvements et la saisie d'inventaire : la
        // validation d'un inventaire avait deja sa propre cle (inventory.validate).
        Target(PermissionCatalog.InventoryItemManage, DomainInventory, "Gerer le referentiel des stocks",
            "Creer, modifier, activer ou desactiver les magasins et les articles.", PermissionCatalog.InventoryWrite),
        Target(PermissionCatalog.InventoryMovementRecord, DomainInventory, "Enregistrer les mouvements de stock",
            "Enregistrer les entrees, sorties, ajustements et transferts entre magasins.", PermissionCatalog.InventoryWrite),
        Target(PermissionCatalog.InventoryCountManage, DomainInventory, "Saisir les inventaires physiques",
            "Ouvrir un inventaire physique et saisir ses lignes de comptage.", PermissionCatalog.InventoryWrite),
        Target(PermissionCatalog.InventoryCountValidate, DomainInventory, "Valider les inventaires",
            "Valider un inventaire physique : generer les mouvements d'ajustement et le figer definitivement.", PermissionCatalog.InventoryValidate),

        // ------------------------------ 12 Achats & Fournisseurs ------------------------------
        Target(PermissionCatalog.PurchasingOrderRead, DomainPurchasing, "Lire les achats",
            "Consulter le referentiel fournisseurs, les bons de commande et l'avancement des receptions.", PermissionCatalog.PurchasingRead),
        Target(PermissionCatalog.PurchasingSupplierManage, DomainPurchasing, "Gerer les fournisseurs",
            "Creer, modifier, activer ou desactiver les fournisseurs.", PermissionCatalog.PurchasingWrite),
        Target(PermissionCatalog.PurchasingOrderManage, DomainPurchasing, "Saisir les bons de commande",
            "Saisir un bon de commande en brouillon, modifier ses lignes et l'annuler avec motif.", PermissionCatalog.PurchasingWrite),
        Target(PermissionCatalog.PurchasingOrderApprove, DomainPurchasing, "Approuver les bons de commande",
            "Approuver un bon de commande : allouer son numero definitif et figer ses lignes - l'acte qui engage la depense.", PermissionCatalog.PurchasingApprove),
        Target(PermissionCatalog.PurchasingReceiptExecute, DomainPurchasing, "Receptionner les marchandises",
            "Enregistrer une reception contre un bon de commande approuve et generer l'entree en stock correspondante.", PermissionCatalog.PurchasingReceive),

        // ------------------------------ 13 Ressources Humaines & Paie ------------------------------
        Target(PermissionCatalog.HrEmployeeRead, DomainHumanResources, "Lire les ressources humaines",
            "Consulter les departements, les postes, les dossiers collaborateurs, les contrats, les pointages, les absences et la paie.", PermissionCatalog.HrRead),
        Target(PermissionCatalog.HrEmployeeManage, DomainHumanResources, "Gerer les collaborateurs",
            "Creer et modifier les departements, les postes, les dossiers collaborateurs et les contrats.", PermissionCatalog.HrWrite),
        Target(PermissionCatalog.HrTimeManage, DomainHumanResources, "Gerer le temps et les absences",
            "Saisir et valider les pointages, declarer, approuver, refuser ou annuler les absences.", PermissionCatalog.HrWrite),
        Target(PermissionCatalog.HrPayrollProcess, DomainHumanResources, "Preparer la paie",
            "Parametrer les baremes legaux, saisir les primes, generer la pre-paie et valider les bulletins.", PermissionCatalog.HrPayroll),
        // Deja au format cible : la cle est sa propre cible, sans cle historique a couvrir.
        Target(PermissionCatalog.HrPayrollClose, DomainHumanResources, "Cloturer la paie",
            "Valider une periode de paie puis la cloturer definitivement, ce qui verrouille le mois."),

        // ------------------------------ Workflow (rendu dans 01 Mon Espace, configure dans 02) ------------------------------
        Target(PermissionCatalog.WorkflowRequestRead, DomainMonEspace, "Lire les validations",
            "Consulter les circuits de validation et les demandes d'approbation.", PermissionCatalog.ApprovalsRead),
        Target(PermissionCatalog.WorkflowCircuitManage, DomainAdministration, "Gerer les circuits de validation",
            "Creer et modifier les circuits de validation, les activer ou les desactiver, et ouvrir une demande.", PermissionCatalog.ApprovalsWrite),
        Target(PermissionCatalog.WorkflowRequestDecide, DomainMonEspace, "Decider des validations",
            "Approuver ou rejeter une etape de validation qui vous est assignee.", PermissionCatalog.ApprovalsDecide),

        // ------------------------------ 20 Pilotage, KPI & BI ------------------------------
        Target(PermissionCatalog.PilotageDashboardRead, DomainPilotage, "Lire les tableaux de bord",
            "Consulter les tableaux de bord de direction, le dashboard groupe, le cockpit DEC et les indicateurs.", PermissionCatalog.DashboardRead),
        Target(PermissionCatalog.PilotageReportExecute, DomainPilotage, "Executer les rapports",
            "Consulter le catalogue des rapports, les executer et consulter le journal d'execution.", PermissionCatalog.ReportsRead),
        Target(PermissionCatalog.PilotageReportExport, DomainPilotage, "Exporter les rapports",
            "Exporter ou imprimer les etats.", PermissionCatalog.ReportsExport),
        Target(PermissionCatalog.PilotageKpiAdmin, DomainPilotage, "Parametrer la bibliotheque KPI",
            "Fixer les seuils et objectifs des indicateurs, rattacher les comptes aux groupes de gestion et cloturer les instantanes.", PermissionCatalog.KpiAdmin),

        // ------------------------------ Audit et 22 Administration Systeme ------------------------------
        Target(PermissionCatalog.AuditLogRead, DomainSystem, "Lire le journal d'audit",
            "Consulter le journal des actions sensibles.", PermissionCatalog.AuditRead),
        Target(PermissionCatalog.SystemBackupRead, DomainSystem, "Lire les sauvegardes",
            "Consulter l'etat des sauvegardes et la politique de retention.", PermissionCatalog.MaintenanceRead),
        Target(PermissionCatalog.SystemBackupExecute, DomainSystem, "Declencher une sauvegarde",
            "Declencher manuellement une sauvegarde de la base de donnees.", PermissionCatalog.MaintenanceBackup),
        Target(PermissionCatalog.SystemWorkstationRead, DomainSystem, "Lire le registre des postes",
            "Consulter les postes declares, leur dernier contact et les erreurs remontees par les clients.", PermissionCatalog.SyncRead)
    };

    private static readonly IReadOnlyDictionary<string, TargetPermissionDefinition> ByKey =
        All.ToDictionary(target => target.Key, StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, string[]> TargetKeysByLegacyKey =
        All
            .SelectMany(target => target.LegacyKeys.Select(legacyKey => (legacyKey, target.Key)))
            .GroupBy(pair => pair.legacyKey, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(pair => pair.Key).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

    /// <summary>Les cles historiques couvertes par au moins une cle cible (82 : tout le catalogue historique sauf hr.payroll.close, qui est sa propre cible).</summary>
    public static IReadOnlyCollection<string> LegacyKeys { get; } =
        TargetKeysByLegacyKey.Keys.Order(StringComparer.Ordinal).ToArray();

    public static bool IsTargetKey(string key) => ByKey.ContainsKey(key);

    public static bool IsLegacyKey(string key) => TargetKeysByLegacyKey.ContainsKey(key);

    public static TargetPermissionDefinition? Find(string targetKey) =>
        ByKey.TryGetValue(targetKey, out var target) ? target : null;

    /// <summary>Les cles cibles qu'une cle historique couvre - vide si la cle n'est pas historique.</summary>
    public static IReadOnlyCollection<string> TargetKeysCoveredBy(string legacyKey) =>
        TargetKeysByLegacyKey.TryGetValue(legacyKey, out var targets) ? targets : Array.Empty<string>();

    /// <summary>Les cles historiques qui couvrent une cle cible - vide si la cle n'est pas une cible ou n'a pas d'historique.</summary>
    public static IReadOnlyCollection<string> LegacyKeysCovering(string targetKey) =>
        ByKey.TryGetValue(targetKey, out var target) ? target.LegacyKeys : Array.Empty<string>();

    /// <summary>
    /// Une cle historique est 1:1 quand elle ne couvre qu'une seule cle cible : les deux sont
    /// alors equivalentes dans les deux sens. Sinon elle est composite.
    /// </summary>
    public static bool IsOneToOne(string legacyKey) => TargetKeysCoveredBy(legacyKey).Count == 1;

    /// <summary>
    /// La liste des claims <c>permission</c> dont UN SEUL suffit a satisfaire la politique nommee
    /// par cette cle. C'est la regle unique que Program.cs (politiques d'autorisation) et
    /// SecurityContextExtensions.HasPermission (leviers optionnels) appliquent :
    /// <list type="bullet">
    ///   <item>cle cible : elle-meme, ou n'importe quelle cle historique qui la couvre ;</item>
    ///   <item>cle historique 1:1 : exactement la politique de sa cle cible (dont elle fait
    ///         partie) - retaguer une route de l'une vers l'autre ne change donc jamais qui y
    ///         accede ;</item>
    ///   <item>cle historique composite : elle-meme seulement - une cle fine ne vaut jamais la
    ///         cle large ;</item>
    ///   <item>cle inconnue du registre : elle-meme, comme avant le registre.</item>
    /// </list>
    /// </summary>
    public static IReadOnlyCollection<string> AcceptedClaims(string key)
    {
        if (ByKey.TryGetValue(key, out var target))
        {
            return new[] { target.Key }
                .Concat(target.LegacyKeys)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        if (TargetKeysByLegacyKey.TryGetValue(key, out var targets) && targets.Length == 1)
        {
            return AcceptedClaims(targets[0]);
        }

        return new[] { key };
    }

    private static TargetPermissionDefinition Target(
        string key,
        string domain,
        string name,
        string description,
        params string[] legacyKeys)
    {
        var segments = key.Split('.');

        if (segments.Length != 3)
        {
            throw new InvalidOperationException(
                $"La cle cible '{key}' doit avoir la forme domaine.ressource.action.");
        }

        return new TargetPermissionDefinition(key, domain, segments[1], segments[2], name, description, legacyKeys);
    }
}
