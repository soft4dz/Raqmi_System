namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// One factual direction alert: a type, the unit concerned, how many objects match, a severity
/// and the RULE that produced it, spelled out in <see cref="Rule"/> so the reader never has to
/// guess what was counted. No locally invented threshold: each type reuses a rule that already
/// exists in the owning module (the closing obligation, the 48-hour validation wait, the aging
/// module's over-60-days brackets).
/// </summary>
public sealed record GroupAlert(
    GroupAlertType Type,
    string HotelUnitCode,
    string HotelUnitName,
    int Count,
    GroupAlertSeverity Severity,
    string Rule);
