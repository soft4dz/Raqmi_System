namespace RaqmiSystem.Domain.Kpi;

/// <summary>
/// Grain d'une periode d'analyse. Il ne change RIEN au calcul - le moteur ne connait que des
/// bornes [du, au] - mais il est conserve sur l'instantane historise pour qu'un point d'
/// historique dise de lui-meme ce qu'il resume : "septembre 2026" plutot que
/// "2026-09-01 a 2026-09-30", et surtout pour qu'on ne compare jamais un point mensuel a un
/// point trimestriel dans la meme courbe.
/// </summary>
public enum KpiPeriodGranularity
{
    Day = 1,

    Week = 2,

    Month = 3,

    Quarter = 4,

    Year = 5,

    /// <summary>Bornes libres choisies par l'utilisateur ; jamais historisees automatiquement.</summary>
    Custom = 6
}
