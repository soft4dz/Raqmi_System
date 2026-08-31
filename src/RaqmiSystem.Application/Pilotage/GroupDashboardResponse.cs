namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// The whole CEO dashboard in one payload: the group KPIs of the requested period, the same
/// KPIs of the equivalent period one year earlier, the year-over-year variations, the ranked
/// per-unit table and the factual direction alerts. <see cref="Basis"/> spells out, in the
/// server's own words, exactly what every figure counts - the same honesty device as
/// AgingBalanceResponse.AgingBasis in the receivables module.
/// </summary>
public sealed record GroupDashboardResponse(
    DateOnly From,
    DateOnly To,
    DateOnly PreviousFrom,
    DateOnly PreviousTo,
    GroupKpiSet Kpis,
    GroupKpiSet PreviousKpis,
    GroupKpiVariations Variations,
    IReadOnlyCollection<GroupUnitRow> Units,
    IReadOnlyCollection<GroupAlert> Alerts,
    GroupDashboardBasis Basis);
