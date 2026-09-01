namespace RaqmiSystem.Domain.Kpi;

/// <summary>
/// Gravite d'une alerte KPI. Elle est DEDUITE du verdict de sante (<see cref="KpiHealth"/>) et
/// jamais saisie : une alerte n'existe que parce qu'une valeur a franchi un seuil configure, et
/// c'est le seuil franchi qui dit si la situation demande de la vigilance ou une decision.
/// </summary>
public enum KpiAlertSeverity
{
    /// <summary>Valeur entre les deux bornes : a surveiller.</summary>
    Watch = 1,

    /// <summary>Valeur au-dela du seuil critique : appelle une decision.</summary>
    Critical = 2
}
