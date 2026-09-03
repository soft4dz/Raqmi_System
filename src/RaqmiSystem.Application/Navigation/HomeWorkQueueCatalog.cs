using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Application.Navigation;

/// <summary>Bande d'urgence d'une file de travail de l'accueil.</summary>
public enum HomeBand
{
    Overdue,
    Today,
    Watch
}

/// <summary>Périmètre du chiffre porté par une file : ce que la pastille de la carte annonce.</summary>
public enum HomeScope
{
    /// <summary>L'unité du poste (réglage local) : la route exige <c>hotelUnitCode</c>.</summary>
    Unit,

    /// <summary>Groupe entier : aucune affectation utilisateur ↔ unité n'existe côté serveur.</summary>
    Group,

    /// <summary>Filtré par le serveur sur les rôles du jeton (« Ma décision »).</summary>
    Me,

    /// <summary>Le système lui-même (sauvegardes, postes).</summary>
    System
}

/// <summary>Ce que la carte propose : agir, suivre, ou seulement informer.</summary>
public enum HomeMode
{
    Act,
    Watch,
    Information
}

/// <summary>Les sections de « Mon travail », dans l'ordre de rendu.</summary>
public enum HomeSectionKind
{
    Banner,
    Overdue,
    Today,
    Watch,
    RecentScreens,
    Product
}

/// <summary>Pourquoi une bande n'a rien à montrer : l'état vide le dit, il ne prétend jamais « rien à faire » quand rien n'a été lu.</summary>
public enum HomeEmptyReason
{
    None,
    NoQueues,
    UnitMissing
}

/// <summary>D'où vient la bande d'une file : le registre, un booléen serveur, ou un placement éditorial documenté.</summary>
public enum HomeBandBasis
{
    Registry,
    ServerFlag,
    Editorial
}

/// <summary>
/// Une valeur par appel réseau, dans l'ORDRE D'APPEL : du plus léger au plus lourd. La vue
/// enchaîne les sources dans cet ordre, une <c>RunAsync</c> par source, pour que les cartes
/// les moins coûteuses se remplissent d'abord.
/// </summary>
public enum HomeSource
{
    BusinessDate,
    PendingApprovals,
    FrontDesk,
    ArrivalBoard,
    DepartureBoard,
    HousekeepingBoard,
    RevenueSummary,
    ReceiptsDraft,
    ReceiptsConfirmed,
    LowStock,
    PaymentOrdersApproved,
    PurchaseOrdersDraft,
    PurchaseOrdersApproved,
    InventoryCountsDraft,
    AbsencesRequested,
    PayrollPeriods,
    EventsToday,
    HaccpReadings,
    BackupStatus,
    Workstations,
    UnitDashboardYesterday,
    Aging,
    DecCockpit
}

/// <summary>
/// Une file de travail de l'accueil.
/// </summary>
/// <param name="ReadKey">Clé (cible) dont la détention compose la carte.</param>
/// <param name="ActKey">Clé (cible) qui donne le verbe ; nulle = file d'information.</param>
/// <param name="UnitWhenKnown">Périmètre <see cref="HomeScope.Unit"/> quand le poste a une unité, <see cref="HomeScope.Group"/> sinon — jamais ignorée faute d'unité.</param>
/// <param name="TargetReadKey">Clé de lecture de l'onglet cible (celle d'<c>ApplyModuleAccess</c>).</param>
/// <param name="FallbackTab">Onglet de repli quand la cible est fermée au profil.</param>
public sealed record HomeWorkQueueDefinition(
    string Id,
    string Label,
    HomeBand Band,
    HomeBandBasis BandBasis,
    HomeScope Scope,
    bool UnitWhenKnown,
    string ReadKey,
    string? ActKey,
    HomeSource Source,
    int TargetTab,
    string TargetReadKey,
    int? FallbackTab,
    string? FallbackReadKey,
    string ActVerb,
    string WatchVerb);

/// <summary>
/// Route servie par une source : documentaire, pour le test de registre qui vérifie que chaque
/// route existe dans l'API et que sa politique est satisfaite par la clé de lecture déclarée.
/// </summary>
public sealed record HomeSourceRoute(HomeSource Source, string Route, string ReadKey, string ClientMethod);

/// <summary>
/// Registre des files de travail de l'accueil « Mon Espace · Mon travail » : la source unique
/// de ce que l'onglet 0 peut montrer.
/// </summary>
/// <remarks>
/// Chaque file est adossée à une route existante et à une méthode déjà présente dans le client
/// API. Aucune règle métier n'est écrite ici : la bande vient du registre ou d'un booléen que le
/// serveur renvoie (<c>IsLate</c>, <c>IsOverdue</c>) ; les trois placements éditoriaux dans
/// « En retard » sont assumés et documentés (<see cref="HomeBandBasis.Editorial"/>) : des journées
/// métier passées non clôturées, des recettes rejetées par la DEC, et la tranche « plus de 90
/// jours » que le serveur calcule lui-même.
///
/// Ce qui n'y figure pas, et n'y sera jamais présenté comme une fonction : tâches transverses,
/// notifications, messagerie, agenda, favoris, demandes, délégations — aucune route serveur.
/// </remarks>
public static class HomeWorkQueueCatalog
{
    // Onglets cibles de MainTabs (voir l'ordre commenté dans MainWindow.Navigation.cs).
    private const int RevenueTab = 2;
    private const int DashboardTab = 3;
    private const int ClosingTab = 5;
    private const int TreasuryTab = 6;
    private const int ReceivablesTab = 13;
    private const int ApprovalsTab = 16;
    private const int BackupTab = 18;
    private const int DecCockpitTab = 20;
    private const int HousekeepingTab = 21;
    private const int HumanResourcesTab = 22;
    private const int InventoryTab = 24;
    private const int PurchasingTab = 25;
    private const int KitchenTab = 26;
    private const int SyncTab = 27;
    private const int MiceTab = 28;
    private const int PmsTab = 30;

    private const string See = "Voir";

    public static IReadOnlyList<HomeWorkQueueDefinition> Queues { get; } =
    [
        // ------------------------------------------------------------- En retard
        Queue("arrivals-late", "Arrivées en retard", HomeBand.Overdue, HomeBandBasis.ServerFlag, HomeScope.Unit,
            PermissionCatalog.LodgingFrontOfficeRead, PermissionCatalog.LodgingCheckinExecute, HomeSource.FrontDesk,
            PmsTab, PermissionCatalog.LodgingRead, act: "Traiter"),
        Queue("departures-late", "Départs en retard", HomeBand.Overdue, HomeBandBasis.ServerFlag, HomeScope.Unit,
            PermissionCatalog.LodgingFrontOfficeRead, PermissionCatalog.LodgingCheckoutExecute, HomeSource.FrontDesk,
            PmsTab, PermissionCatalog.LodgingRead, act: "Traiter"),
        Queue("closing-unit", "Journées à clôturer", HomeBand.Overdue, HomeBandBasis.ServerFlag, HomeScope.Unit,
            PermissionCatalog.LodgingFrontOfficeRead, PermissionCatalog.LodgingClosingClose, HomeSource.BusinessDate,
            ClosingTab, PermissionCatalog.ClosingRead, act: "Clôturer",
            fallbackTab: PmsTab, fallbackReadKey: PermissionCatalog.LodgingRead),
        Queue("dec-backlog", "Journées non clôturées", HomeBand.Overdue, HomeBandBasis.Editorial, HomeScope.Group,
            PermissionCatalog.PilotageDashboardRead, PermissionCatalog.LodgingClosingClose, HomeSource.DecCockpit,
            DecCockpitTab, PermissionCatalog.DashboardRead, act: "Ouvrir le cockpit"),
        Queue("dec-rejected", "Recettes rejetées à corriger", HomeBand.Overdue, HomeBandBasis.Editorial, HomeScope.Group,
            PermissionCatalog.PilotageDashboardRead, PermissionCatalog.FinanceRevenueRecord, HomeSource.DecCockpit,
            RevenueTab, PermissionCatalog.RevenueRead, act: "Corriger",
            fallbackTab: DecCockpitTab, fallbackReadKey: PermissionCatalog.DashboardRead),
        Queue("aging-90", "Créances à plus de 90 jours", HomeBand.Overdue, HomeBandBasis.Editorial, HomeScope.Group,
            PermissionCatalog.FinanceReceivableRead, actKey: null, HomeSource.Aging,
            ReceivablesTab, PermissionCatalog.ReceivablesRead, act: See, watch: "Voir la balance"),
        Queue("backup", "Sauvegarde", HomeBand.Overdue, HomeBandBasis.ServerFlag, HomeScope.System,
            PermissionCatalog.SystemBackupRead, PermissionCatalog.SystemBackupExecute, HomeSource.BackupStatus,
            BackupTab, PermissionCatalog.MaintenanceRead, act: "Sauvegarder"),

        // ------------------------------------------------------------ Aujourd'hui
        Queue("arrivals", "Arrivées du jour", HomeBand.Today, HomeBandBasis.Registry, HomeScope.Unit,
            PermissionCatalog.LodgingFrontOfficeRead, PermissionCatalog.LodgingCheckinExecute, HomeSource.FrontDesk,
            PmsTab, PermissionCatalog.LodgingRead, act: "Ouvrir les arrivées"),
        Queue("arrivals-unassigned", "Arrivées sans chambre affectée", HomeBand.Today, HomeBandBasis.Registry, HomeScope.Unit,
            PermissionCatalog.LodgingFrontOfficeRead, PermissionCatalog.LodgingCheckinExecute, HomeSource.ArrivalBoard,
            PmsTab, PermissionCatalog.LodgingRead, act: "Affecter"),
        Queue("departures", "Départs du jour", HomeBand.Today, HomeBandBasis.Registry, HomeScope.Unit,
            PermissionCatalog.LodgingFrontOfficeRead, PermissionCatalog.LodgingCheckoutExecute, HomeSource.FrontDesk,
            PmsTab, PermissionCatalog.LodgingRead, act: "Ouvrir les départs"),
        Queue("departures-balance", "Départs avec solde à régler", HomeBand.Today, HomeBandBasis.Registry, HomeScope.Unit,
            PermissionCatalog.LodgingFrontOfficeRead, PermissionCatalog.LodgingCheckoutExecute, HomeSource.DepartureBoard,
            PmsTab, PermissionCatalog.LodgingRead, act: "Encaisser"),
        Queue("hk-dirty", "Chambres à préparer", HomeBand.Today, HomeBandBasis.Registry, HomeScope.Unit,
            PermissionCatalog.HousekeepingTaskRead, PermissionCatalog.HousekeepingTaskManage, HomeSource.HousekeepingBoard,
            HousekeepingTab, PermissionCatalog.HousekeepingRead, act: "Ouvrir le tableau"),
        Queue("hk-inspect", "Chambres à inspecter", HomeBand.Today, HomeBandBasis.Registry, HomeScope.Unit,
            PermissionCatalog.HousekeepingTaskRead, PermissionCatalog.HousekeepingRoomInspect, HomeSource.HousekeepingBoard,
            HousekeepingTab, PermissionCatalog.HousekeepingRead, act: "Inspecter"),
        // La route /pending exige la clé de DÉCISION et répond 403 à approvals.read seul : la
        // clé de lecture de cette file est donc la clé de décision, jamais approvals.read.
        Queue("approvals", "Validations en attente de ma décision", HomeBand.Today, HomeBandBasis.Registry, HomeScope.Me,
            PermissionCatalog.WorkflowRequestDecide, PermissionCatalog.WorkflowRequestDecide, HomeSource.PendingApprovals,
            ApprovalsTab, PermissionCatalog.ApprovalsRead, act: "Décider"),
        Queue("dec-revenue", "Recettes à valider (DEC)", HomeBand.Today, HomeBandBasis.Registry, HomeScope.Group,
            PermissionCatalog.PilotageDashboardRead, PermissionCatalog.FinanceRevenueValidate, HomeSource.DecCockpit,
            RevenueTab, PermissionCatalog.RevenueRead, act: "Valider",
            fallbackTab: DecCockpitTab, fallbackReadKey: PermissionCatalog.DashboardRead),
        Queue("dec-po", "Ordres de paiement à approuver", HomeBand.Today, HomeBandBasis.Registry, HomeScope.Group,
            PermissionCatalog.PilotageDashboardRead, PermissionCatalog.FinancePaymentOrderApprove, HomeSource.DecCockpit,
            TreasuryTab, PermissionCatalog.TreasuryRead, act: "Approuver",
            fallbackTab: DecCockpitTab, fallbackReadKey: PermissionCatalog.DashboardRead),
        Queue("revenue-yesterday", "Recettes J-1", HomeBand.Today, HomeBandBasis.Registry, HomeScope.Group,
            PermissionCatalog.PilotageDashboardRead, actKey: null, HomeSource.UnitDashboardYesterday,
            DashboardTab, PermissionCatalog.DashboardRead, act: See, watch: "Voir le tableau"),
        Queue("revenue-draft", "Recettes en brouillon à soumettre", HomeBand.Today, HomeBandBasis.Registry, HomeScope.Unit,
            PermissionCatalog.FinanceRevenueRead, PermissionCatalog.FinanceRevenueRecord, HomeSource.RevenueSummary,
            RevenueTab, PermissionCatalog.RevenueRead, act: "Soumettre"),
        Queue("po-pay", "Ordres de paiement à régler", HomeBand.Today, HomeBandBasis.Registry, HomeScope.Group,
            PermissionCatalog.FinanceTreasuryRead, PermissionCatalog.FinancePaymentOrderManage, HomeSource.PaymentOrdersApproved,
            TreasuryTab, PermissionCatalog.TreasuryRead, act: "Régler"),
        Queue("receipts-draft", "Encaissements en brouillon", HomeBand.Today, HomeBandBasis.Registry, HomeScope.Group,
            PermissionCatalog.FinanceTreasuryRead, PermissionCatalog.FinanceReceiptManage, HomeSource.ReceiptsDraft,
            TreasuryTab, PermissionCatalog.TreasuryRead, act: "Confirmer", unitWhenKnown: true),
        Queue("receipts-today", "Encaissé aujourd'hui", HomeBand.Today, HomeBandBasis.Registry, HomeScope.Group,
            PermissionCatalog.FinanceTreasuryRead, actKey: null, HomeSource.ReceiptsConfirmed,
            TreasuryTab, PermissionCatalog.TreasuryRead, act: See, unitWhenKnown: true),
        Queue("counts-draft", "Inventaires à valider", HomeBand.Today, HomeBandBasis.Registry, HomeScope.Group,
            PermissionCatalog.InventoryStockRead, PermissionCatalog.InventoryCountValidate, HomeSource.InventoryCountsDraft,
            InventoryTab, PermissionCatalog.InventoryRead, act: "Valider"),
        Queue("po-approve", "Commandes d'achat à approuver", HomeBand.Today, HomeBandBasis.Registry, HomeScope.Group,
            PermissionCatalog.PurchasingOrderRead, PermissionCatalog.PurchasingOrderApprove, HomeSource.PurchaseOrdersDraft,
            PurchasingTab, PermissionCatalog.PurchasingRead, act: "Approuver"),
        Queue("po-receive", "Commandes à réceptionner", HomeBand.Today, HomeBandBasis.Registry, HomeScope.Group,
            PermissionCatalog.PurchasingOrderRead, PermissionCatalog.PurchasingReceiptExecute, HomeSource.PurchaseOrdersApproved,
            PurchasingTab, PermissionCatalog.PurchasingRead, act: "Réceptionner"),
        Queue("haccp", "Relevés HACCP non conformes", HomeBand.Today, HomeBandBasis.Registry, HomeScope.Group,
            PermissionCatalog.FnbKitchenRead, PermissionCatalog.FnbKitchenManage, HomeSource.HaccpReadings,
            KitchenTab, PermissionCatalog.KitchenRead, act: "Traiter"),
        Queue("absences", "Absences à approuver", HomeBand.Today, HomeBandBasis.Registry, HomeScope.Group,
            PermissionCatalog.HrEmployeeRead, PermissionCatalog.HrTimeManage, HomeSource.AbsencesRequested,
            HumanResourcesTab, PermissionCatalog.HrRead, act: "Approuver"),
        Queue("payroll", "Bulletins en brouillon", HomeBand.Today, HomeBandBasis.Registry, HomeScope.Group,
            PermissionCatalog.HrEmployeeRead, PermissionCatalog.HrPayrollProcess, HomeSource.PayrollPeriods,
            HumanResourcesTab, PermissionCatalog.HrRead, act: "Ouvrir la paie"),
        Queue("events-today", "Événements du jour", HomeBand.Today, HomeBandBasis.Registry, HomeScope.Unit,
            PermissionCatalog.MiceEventRead, actKey: null, HomeSource.EventsToday,
            MiceTab, PermissionCatalog.MiceRead, act: See),

        // ------------------------------------------------------------ À surveiller
        Queue("hk-ooo", "Chambres hors service", HomeBand.Watch, HomeBandBasis.Registry, HomeScope.Unit,
            PermissionCatalog.HousekeepingTaskRead, actKey: null, HomeSource.HousekeepingBoard,
            HousekeepingTab, PermissionCatalog.HousekeepingRead, act: See),
        Queue("low-stock", "Articles sous le minimum", HomeBand.Watch, HomeBandBasis.Registry, HomeScope.Group,
            PermissionCatalog.InventoryStockRead, actKey: null, HomeSource.LowStock,
            InventoryTab, PermissionCatalog.InventoryRead, act: See),
        Queue("workstations", "Postes en service", HomeBand.Watch, HomeBandBasis.Registry, HomeScope.System,
            PermissionCatalog.SystemWorkstationRead, actKey: null, HomeSource.Workstations,
            SyncTab, PermissionCatalog.SyncRead, act: See)
    ];

    /// <summary>Les routes appelées par les sources, pour le test de registre.</summary>
    public static IReadOnlyList<HomeSourceRoute> Routes { get; } =
    [
        new(HomeSource.BusinessDate, "/api/v1/lodging/business-date", PermissionCatalog.LodgingFrontOfficeRead, "GetBusinessDateAsync"),
        new(HomeSource.PendingApprovals, "/api/v1/approvals/instances/pending", PermissionCatalog.WorkflowRequestDecide, "GetPendingApprovalInstancesAsync"),
        new(HomeSource.FrontDesk, "/api/v1/lodging/front-desk", PermissionCatalog.LodgingFrontOfficeRead, "GetFrontDeskAsync"),
        new(HomeSource.ArrivalBoard, "/api/v1/lodging/arrivals", PermissionCatalog.LodgingFrontOfficeRead, "GetArrivalsAsync"),
        new(HomeSource.DepartureBoard, "/api/v1/lodging/departures", PermissionCatalog.LodgingFrontOfficeRead, "GetDeparturesAsync"),
        new(HomeSource.HousekeepingBoard, "/api/v1/housekeeping/board", PermissionCatalog.HousekeepingTaskRead, "GetHousekeepingBoardAsync"),
        new(HomeSource.RevenueSummary, "/api/v1/revenue/daily/summary", PermissionCatalog.FinanceRevenueRead, "GetDailyRevenueSummaryAsync"),
        new(HomeSource.ReceiptsDraft, "/api/v1/treasury/receipts/summary", PermissionCatalog.FinanceTreasuryRead, "GetCashReceiptSummaryAsync"),
        new(HomeSource.ReceiptsConfirmed, "/api/v1/treasury/receipts/summary", PermissionCatalog.FinanceTreasuryRead, "GetCashReceiptSummaryAsync"),
        new(HomeSource.LowStock, "/api/v1/inventory/low-stock", PermissionCatalog.InventoryStockRead, "GetLowStockAsync"),
        new(HomeSource.PaymentOrdersApproved, "/api/v1/treasury/payment-orders", PermissionCatalog.FinanceTreasuryRead, "GetPaymentOrdersAsync"),
        new(HomeSource.PurchaseOrdersDraft, "/api/v1/purchasing/orders", PermissionCatalog.PurchasingOrderRead, "GetPurchaseOrdersAsync"),
        new(HomeSource.PurchaseOrdersApproved, "/api/v1/purchasing/orders", PermissionCatalog.PurchasingOrderRead, "GetPurchaseOrdersAsync"),
        new(HomeSource.InventoryCountsDraft, "/api/v1/inventory/counts", PermissionCatalog.InventoryStockRead, "GetInventoryCountsAsync"),
        new(HomeSource.AbsencesRequested, "/api/v1/hr/absences", PermissionCatalog.HrEmployeeRead, "GetHrAbsencesAsync"),
        new(HomeSource.PayrollPeriods, "/api/v1/hr/payroll/periods", PermissionCatalog.HrEmployeeRead, "GetPayrollPeriodsAsync"),
        new(HomeSource.EventsToday, "/api/v1/mice/events", PermissionCatalog.MiceEventRead, "GetEventsAsync"),
        new(HomeSource.HaccpReadings, "/api/v1/kitchen/readings", PermissionCatalog.FnbKitchenRead, "GetTemperatureReadingsAsync"),
        new(HomeSource.BackupStatus, "/api/v1/maintenance/backups/status", PermissionCatalog.SystemBackupRead, "GetBackupStatusAsync"),
        new(HomeSource.Workstations, "/api/v1/sync/stations", PermissionCatalog.SystemWorkstationRead, "GetWorkstationsAsync"),
        new(HomeSource.UnitDashboardYesterday, "/api/v1/revenue/daily/dashboard", PermissionCatalog.PilotageDashboardRead, "GetUnitDashboardAsync"),
        new(HomeSource.Aging, "/api/v1/receivables/aging", PermissionCatalog.FinanceReceivableRead, "GetAgingBalanceAsync"),
        new(HomeSource.DecCockpit, "/api/v1/pilotage/dec-cockpit", PermissionCatalog.PilotageDashboardRead, "GetDecCockpitAsync")
    ];

    private static readonly IReadOnlyDictionary<string, HomeWorkQueueDefinition> ById =
        Queues.ToDictionary(queue => queue.Id, StringComparer.Ordinal);

    public static HomeWorkQueueDefinition Find(string id) =>
        ById.TryGetValue(id, out var queue)
            ? queue
            : throw new KeyNotFoundException($"La file de travail '{id}' n'existe pas dans le registre.");

    private static HomeWorkQueueDefinition Queue(
        string id,
        string label,
        HomeBand band,
        HomeBandBasis basis,
        HomeScope scope,
        string readKey,
        string? actKey,
        HomeSource source,
        int targetTab,
        string targetReadKey,
        string act,
        string watch = See,
        int? fallbackTab = null,
        string? fallbackReadKey = null,
        bool unitWhenKnown = false) =>
        new(id, label, band, basis, scope, unitWhenKnown, readKey, actKey, source,
            targetTab, targetReadKey, fallbackTab, fallbackReadKey, act, watch);
}
