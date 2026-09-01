namespace RaqmiSystem.Domain.Kpi;

/// <summary>
/// Etat d'un instantane historise.
///
/// C'est la garantie centrale de l'historisation : une valeur rattachee a une cloture
/// officielle ne doit JAMAIS etre reecrite en silence parce qu'une donnee ancienne a bouge.
/// Un instantane <see cref="Provisional"/> est rafraichi par tout recalcul ; un instantane
/// <see cref="Closed"/> est fige pour de bon. Si un recalcul aboutit a une valeur differente
/// d'un instantane cloture, le moteur ne l'ecrase pas : il signale la divergence, exactement
/// comme la comptabilite corrige une ecriture comptabilisee par une extourne et jamais par une
/// modification.
/// </summary>
public enum KpiSnapshotStatus
{
    /// <summary>Provisoire : recalculable et remplacable a volonte.</summary>
    Provisional = 1,

    /// <summary>Cloture : fige. Le moteur ne le reecrit plus, il constate les divergences.</summary>
    Closed = 2
}
