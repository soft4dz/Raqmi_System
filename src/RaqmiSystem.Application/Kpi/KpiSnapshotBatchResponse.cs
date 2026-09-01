namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Le compte rendu d'une pose ou d'une cloture d'instantanes.
///
/// <see cref="Divergences"/> est le champ qui compte : il liste les instantanes DEJA CLOTURES
/// dont le recalcul donne desormais autre chose. Le moteur ne les a pas touches - c'est la
/// regle - et les remonte pour que quelqu'un decide. Un ERP qui corrigerait ces valeurs en
/// silence rendrait toute cloture officielle sans valeur.
/// </summary>
public sealed record KpiSnapshotBatchResponse(
    DateOnly From,
    DateOnly To,
    int Created,
    int Refreshed,
    int Closed,
    int SkippedBecauseClosed,
    IReadOnlyCollection<string> Divergences);
