namespace RaqmiSystem.Domain.Kpi;

/// <summary>
/// Verdict d'un indicateur face a ses seuils. Trois etats, deux bornes : voir
/// <see cref="KpiThreshold"/> pour la raison de ce choix.
/// </summary>
public enum KpiHealth
{
    /// <summary>Aucun seuil configure, ou valeur indisponible : aucun verdict n'est rendu.</summary>
    Unknown = 0,

    /// <summary>La valeur atteint ou depasse le seuil favorable.</summary>
    Favorable = 1,

    /// <summary>La valeur est entre les deux bornes : a surveiller.</summary>
    Watch = 2,

    /// <summary>La valeur atteint ou franchit le seuil critique.</summary>
    Critical = 3
}
