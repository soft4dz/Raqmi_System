namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Demande de cloture des instantanes d'une periode. Acte irreversible : les valeurs figees ne
/// seront plus jamais reecrites par un recalcul.
/// </summary>
public sealed record CloseKpiSnapshotsRequest(
    DateOnly From,
    DateOnly To,
    IReadOnlyCollection<string>? Codes = null);
