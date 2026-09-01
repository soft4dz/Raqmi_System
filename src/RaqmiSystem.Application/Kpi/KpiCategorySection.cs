using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Application.Kpi;

/// <summary>Les indicateurs d'une famille metier, dans l'ordre du catalogue.</summary>
public sealed record KpiCategorySection(
    KpiCategory Category,
    string Label,
    IReadOnlyCollection<KpiMeasureResponse> Measures);
