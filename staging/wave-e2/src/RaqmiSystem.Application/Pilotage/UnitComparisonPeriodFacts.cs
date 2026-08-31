namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// Everything one period contributes to the comparison, fetched by the caller and combined by
/// <see cref="UnitComparisonCalculator"/>: the revenue rows of the period, the receipts of the
/// period, and the stays overlapping the period. Status filtering deliberately does NOT happen
/// here - each fact carries its status and the calculator applies the modules' counting rules
/// in pure code (the EF-backed service may pre-filter in SQL as an optimization, but the
/// rules' single home is the calculator).
///
/// For the PREVIOUS period (N-1) only the revenues are consumed - the comparison confronts
/// last year's turnover, nothing else - so the service passes <see cref="Empty"/>-shaped
/// receipt and stay collections there without loading them.
/// </summary>
public sealed record UnitComparisonPeriodFacts(
    IReadOnlyCollection<UnitComparisonRevenueFact> Revenues,
    IReadOnlyCollection<UnitComparisonReceiptFact> Receipts,
    IReadOnlyCollection<UnitComparisonStayFact> Stays)
{
    public static UnitComparisonPeriodFacts Empty { get; } = new(
        Array.Empty<UnitComparisonRevenueFact>(),
        Array.Empty<UnitComparisonReceiptFact>(),
        Array.Empty<UnitComparisonStayFact>());

    /// <summary>Previous-period shape: only the revenues matter for the N-1 columns.</summary>
    public static UnitComparisonPeriodFacts FromRevenues(
        IReadOnlyCollection<UnitComparisonRevenueFact> revenues)
    {
        return new UnitComparisonPeriodFacts(
            revenues,
            Array.Empty<UnitComparisonReceiptFact>(),
            Array.Empty<UnitComparisonStayFact>());
    }
}
