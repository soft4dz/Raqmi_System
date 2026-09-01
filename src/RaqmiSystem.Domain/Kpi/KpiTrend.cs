namespace RaqmiSystem.Domain.Kpi;

/// <summary>
/// Sens d'evolution par rapport a la periode de reference (N-1). C'est un fait arithmetique -
/// la valeur monte, descend ou ne bouge pas - et non un jugement : le jugement vient de la
/// combinaison avec <see cref="KpiPolarity"/>, faite par le lecteur ou par l'ecran.
/// </summary>
public enum KpiTrend
{
    /// <summary>Pas de reference exploitable : periode N-1 vide, ou valeur courante indisponible.</summary>
    Unknown = 0,

    Up = 1,

    Flat = 2,

    Down = 3
}
