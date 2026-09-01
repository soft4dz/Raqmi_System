namespace RaqmiSystem.Domain.Kpi;

/// <summary>
/// Evenements qui rendent le recalcul d'un indicateur legitime. Combinables : un indicateur
/// d'hebergement se recalcule a la demande, chaque jour ET a la cloture journaliere.
///
/// Ce n'est PAS un ordonnanceur : le moteur calcule toujours a la demande a partir des
/// transactions. Ces declencheurs disent quand un INSTANTANE (<see cref="KpiSnapshot"/>) doit
/// etre pose, c'est-a-dire quand la valeur du moment merite d'etre conservee.
/// </summary>
[Flags]
public enum KpiRefreshTrigger
{
    None = 0,

    /// <summary>Sur demande explicite d'un utilisateur ou d'un ecran.</summary>
    OnDemand = 1,

    /// <summary>Quotidien : l'indicateur a un sens jour par jour.</summary>
    Daily = 2,

    /// <summary>Mensuel : l'indicateur n'a de sens qu'au mois (paie, comptabilite).</summary>
    Monthly = 4,

    /// <summary>A la cloture journaliere d'une unite (module Cloture).</summary>
    OnDailyClosing = 8,

    /// <summary>A la cloture mensuelle (paie cloturee, exercice comptable).</summary>
    OnMonthlyClosing = 16,

    /// <summary>A la suite d'un evenement metier (validation de recette, emission de facture).</summary>
    OnBusinessEvent = 32
}
