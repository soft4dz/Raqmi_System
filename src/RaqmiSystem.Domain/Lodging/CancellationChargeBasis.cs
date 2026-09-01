namespace RaqmiSystem.Domain.Lodging;

/// <summary>Sur quoi se calcule une penalite d'annulation ou de no-show.</summary>
public enum CancellationChargeBasis
{
    /// <summary>Aucune penalite : annulation gratuite.</summary>
    None = 0,

    /// <summary>La premiere nuit du sejour, a son tarif fige.</summary>
    FirstNight = 1,

    /// <summary>Les N premieres nuits du sejour, a leurs tarifs figes.</summary>
    Nights = 2,

    /// <summary>Un pourcentage du total du sejour.</summary>
    PercentOfStay = 3,

    /// <summary>Un montant fixe, quelle que soit la duree.</summary>
    FixedAmount = 4
}
