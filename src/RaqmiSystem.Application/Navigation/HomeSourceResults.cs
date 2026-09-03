using RaqmiSystem.Application.Approvals;
using RaqmiSystem.Application.Housekeeping;
using RaqmiSystem.Application.HumanResources;
using RaqmiSystem.Application.Inventory;
using RaqmiSystem.Application.Kitchen;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Maintenance;
using RaqmiSystem.Application.Mice;
using RaqmiSystem.Application.Pilotage;
using RaqmiSystem.Application.Purchasing;
using RaqmiSystem.Application.Receivables;
using RaqmiSystem.Application.Revenue;
using RaqmiSystem.Application.Sync;
using RaqmiSystem.Application.Treasury;

namespace RaqmiSystem.Application.Navigation;

/// <summary>
/// Sac des réponses reçues pour un chargement de l'accueil : les records de réponse existants,
/// tels que le serveur les renvoie, plus l'ensemble des sources qui n'ont pas répondu.
/// </summary>
/// <remarks>
/// Rien n'est recalculé ici : la projection (<see cref="HomeProjection"/>) lit les champs que le
/// serveur a déjà agrégés. Une source absente (null) est encore en chargement ; une source dans
/// <see cref="Failed"/> bascule toutes ses cartes en « Indisponible ».
/// </remarks>
public sealed class HomeSourceResults
{
    public BusinessDateResponse? BusinessDate { get; set; }

    public IReadOnlyCollection<ApprovalInstanceResponse>? PendingApprovals { get; set; }

    public FrontDeskResponse? FrontDesk { get; set; }

    public ArrivalBoardResponse? ArrivalBoard { get; set; }

    public DepartureBoardResponse? DepartureBoard { get; set; }

    public RoomBoardResponse? HousekeepingBoard { get; set; }

    public DailyRevenueSummaryResponse? RevenueSummary { get; set; }

    /// <summary>Synthèse des encaissements du jour filtrée sur <c>status=Draft</c>.</summary>
    public CashReceiptSummaryResponse? ReceiptsDraft { get; set; }

    /// <summary>Synthèse des encaissements du jour filtrée sur <c>status=Confirmed</c> : la seule dont <c>GrandTotal</c> est lu.</summary>
    public CashReceiptSummaryResponse? ReceiptsConfirmed { get; set; }

    public IReadOnlyCollection<LowStockRow>? LowStock { get; set; }

    public IReadOnlyCollection<PaymentOrderResponse>? PaymentOrdersApproved { get; set; }

    public IReadOnlyCollection<PurchaseOrderResponse>? PurchaseOrdersDraft { get; set; }

    public IReadOnlyCollection<PurchaseOrderResponse>? PurchaseOrdersApproved { get; set; }

    public IReadOnlyCollection<InventoryCountResponse>? InventoryCountsDraft { get; set; }

    public IReadOnlyCollection<AbsenceResponse>? AbsencesRequested { get; set; }

    public IReadOnlyCollection<PayrollPeriodResponse>? PayrollPeriods { get; set; }

    public IReadOnlyCollection<EventBookingResponse>? EventsToday { get; set; }

    public IReadOnlyCollection<TemperatureReadingResponse>? HaccpReadings { get; set; }

    public BackupStatusResponse? BackupStatus { get; set; }

    public WorkstationRegistryResponse? Workstations { get; set; }

    public UnitDashboardResponse? UnitDashboardYesterday { get; set; }

    public AgingBalanceResponse? Aging { get; set; }

    public DecCockpitResponse? DecCockpit { get; set; }

    /// <summary>Sources dont l'appel a échoué (le message est déjà dans le bandeau de session).</summary>
    public ISet<HomeSource> Failed { get; } = new HashSet<HomeSource>();

    /// <summary>La source a-t-elle répondu ? (Faux tant qu'elle charge ou qu'elle a échoué.)</summary>
    public bool IsLoaded(HomeSource source) => source switch
    {
        HomeSource.BusinessDate => BusinessDate is not null,
        HomeSource.PendingApprovals => PendingApprovals is not null,
        HomeSource.FrontDesk => FrontDesk is not null,
        HomeSource.ArrivalBoard => ArrivalBoard is not null,
        HomeSource.DepartureBoard => DepartureBoard is not null,
        HomeSource.HousekeepingBoard => HousekeepingBoard is not null,
        HomeSource.RevenueSummary => RevenueSummary is not null,
        HomeSource.ReceiptsDraft => ReceiptsDraft is not null,
        HomeSource.ReceiptsConfirmed => ReceiptsConfirmed is not null,
        HomeSource.LowStock => LowStock is not null,
        HomeSource.PaymentOrdersApproved => PaymentOrdersApproved is not null,
        HomeSource.PurchaseOrdersDraft => PurchaseOrdersDraft is not null,
        HomeSource.PurchaseOrdersApproved => PurchaseOrdersApproved is not null,
        HomeSource.InventoryCountsDraft => InventoryCountsDraft is not null,
        HomeSource.AbsencesRequested => AbsencesRequested is not null,
        HomeSource.PayrollPeriods => PayrollPeriods is not null,
        HomeSource.EventsToday => EventsToday is not null,
        HomeSource.HaccpReadings => HaccpReadings is not null,
        HomeSource.BackupStatus => BackupStatus is not null,
        HomeSource.Workstations => Workstations is not null,
        HomeSource.UnitDashboardYesterday => UnitDashboardYesterday is not null,
        HomeSource.Aging => Aging is not null,
        HomeSource.DecCockpit => DecCockpit is not null,
        _ => false
    };
}
