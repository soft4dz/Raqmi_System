namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// The group-level KPIs of one period. Every figure obeys the owning module's established
/// counting rule (see <see cref="GroupDashboardCalculator"/> for the rules and their reasons):
/// revenue counts Validated entries only, receipts count Confirmed ones only, receivables
/// count Issued invoices only, occupancy counts every non-cancelled / non-no-show stay.
///
/// <see cref="OccupancyRatePercent"/> is null - not zero - when <see cref="AvailableNights"/>
/// is zero: a rate against no capacity does not exist, exactly like the budget variance
/// percentage against a zero target (BudgetVarianceCalculator), and the consumer displays a
/// dash. The raw <see cref="OccupiedNights"/> / <see cref="AvailableNights"/> pair is exposed
/// so the reader can always see what the rate was computed from.
/// </summary>
public sealed record GroupKpiSet(
    decimal ValidatedRevenue,
    decimal ConfirmedReceipts,
    decimal OutstandingReceivables,
    int OutstandingInvoiceCount,
    int OccupiedNights,
    int AvailableNights,
    decimal? OccupancyRatePercent,
    int ActiveUnitCount);
