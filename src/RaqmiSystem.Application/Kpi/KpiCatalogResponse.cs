namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Le catalogue publie : la bibliotheque complete et ses compteurs.
///
/// <see cref="AwaitingSourceCount"/> est affiche volontairement : il dit combien d'indicateurs
/// sont declares, documentes et prets, mais attendent un module que le produit n'a pas encore.
/// C'est une information de pilotage produit, pas un aveu - et la cacher reviendrait a laisser
/// croire que la bibliotheque est plus complete qu'elle ne l'est.
/// </summary>
public sealed record KpiCatalogResponse(
    int TotalCount,
    int ImplementedCount,
    int AwaitingSourceCount,
    int ReadableCount,
    IReadOnlyCollection<KpiDefinitionResponse> Definitions);
