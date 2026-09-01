namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Une unite dans le comparatif : ses indicateurs, dans l'ordre des colonnes demandees. La
/// ligne du groupe est rendue avec un code d'unite nul, pour que l'ecran puisse la fixer en
/// tete de tableau comme reference.
/// </summary>
public sealed record KpiComparisonRow(
    string? HotelUnitCode,
    string HotelUnitName,
    bool IsActive,
    IReadOnlyCollection<KpiMeasureResponse> Measures);
