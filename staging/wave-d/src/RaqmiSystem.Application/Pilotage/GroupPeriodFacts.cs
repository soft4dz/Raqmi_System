namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// Everything one period contributes to the dashboard, fetched by the caller and combined by
/// <see cref="GroupDashboardCalculator"/>: the revenue rows of the period, the receipts of the
/// period, the invoices dated on or before the period's end, and the stays overlapping the
/// period. Status filtering deliberately does NOT happen here - each fact carries its status
/// and the calculator applies the modules' counting rules in pure code (the EF-backed service
/// may pre-filter in SQL as an optimization, but the rules' single home is the calculator).
/// </summary>
public sealed record GroupPeriodFacts(
    IReadOnlyCollection<GroupRevenueFact> Revenues,
    IReadOnlyCollection<GroupReceiptFact> Receipts,
    IReadOnlyCollection<GroupInvoiceFact> Invoices,
    IReadOnlyCollection<GroupStayFact> Stays)
{
    public static GroupPeriodFacts Empty { get; } = new(
        Array.Empty<GroupRevenueFact>(),
        Array.Empty<GroupReceiptFact>(),
        Array.Empty<GroupInvoiceFact>(),
        Array.Empty<GroupStayFact>());
}
