namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// "The state of the group this morning" for the Direction de l'Exploitation et du Controle:
/// the day's work queues in operational priority order (submitted revenue to validate, closing
/// backlog, rejected revenue awaiting correction, payment orders awaiting approval), the
/// per-unit health table for yesterday/today, and the workload indicators. Pure aggregation
/// over the existing modules' data - every figure obeys the owning module's rule (validated
/// revenue only counts as realised, Closed closings only count as closed, Draft payment orders
/// only are pending, non-cancelled / non-no-show reservations block rooms).
/// </summary>
public sealed record DecCockpitResponse(
    DateOnly Date,
    DateOnly Yesterday,
    IReadOnlyCollection<DecPendingValidationUnit> PendingValidations,
    IReadOnlyCollection<DecClosingBacklogUnit> ClosingBacklog,
    IReadOnlyCollection<DecRejectedRevenueItem> RejectedRevenues,
    IReadOnlyCollection<DecPendingPaymentOrderItem> PendingPaymentOrders,
    IReadOnlyCollection<DecUnitHealthRow> UnitHealth,
    int PendingValidationCount,
    decimal PendingValidationAmount,
    int ClosingBacklogDayCount,
    int RejectedCount,
    int PendingPaymentOrderCount,
    decimal PendingPaymentOrderAmount,
    DecClosingDelay? OldestClosingDelay);
