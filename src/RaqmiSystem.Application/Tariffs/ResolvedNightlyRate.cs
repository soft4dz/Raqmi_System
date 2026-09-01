namespace RaqmiSystem.Application.Tariffs;

/// <summary>
/// Le tarif resolu d'UNE nuit : le montant retenu, le plan qui l'a produit, et la convention
/// client appliquee le cas echeant.
///
/// LES CHAMPS DE YIELD SONT EN INIT-ONLY, PAS DANS LE CONSTRUCTEUR POSITIONNEL. La resolution
/// tarifaire de base ne les connait pas ; c'est le moteur de revenue management qui les pose,
/// apres coup, quand il ajuste un prix. Les laisser optionnels permet au module Tarifs de
/// continuer a ne rien savoir du yield, tout en garantissant qu'un prix ajuste PORTE TOUJOURS la
/// regle qui l'a ajuste - une majoration anonyme est indiscutable et irreproductible.
/// </summary>
public sealed record ResolvedNightlyRate(
    decimal Amount,
    string RatePlanCode,
    string? ConventionCustomerCode,
    decimal? DiscountPercent)
{
    /// <summary>Montant avant ajustement de yield. Null quand aucune regle n'a joue.</summary>
    public decimal? BaseAmount { get; init; }

    /// <summary>Code de la regle de yield appliquee. Null quand aucune n'a joue.</summary>
    public string? YieldRuleCode { get; init; }

    /// <summary>Libelle de la regle appliquee, pour l'affichage au comptoir.</summary>
    public string? YieldRuleLabel { get; init; }

    /// <summary>Ajustement applique, en pourcentage. Null quand aucune regle n'a joue.</summary>
    public decimal? YieldAdjustmentPercent { get; init; }

    /// <summary>Vrai quand une regle de revenue management a modifie ce prix.</summary>
    public bool IsYielded => YieldRuleCode is not null;

    /// <summary>Applique un ajustement de yield en conservant la trace de la regle.</summary>
    public ResolvedNightlyRate WithYield(
        decimal adjustedAmount,
        string ruleCode,
        string ruleLabel,
        decimal adjustmentPercent)
    {
        return this with
        {
            Amount = adjustedAmount,
            BaseAmount = Amount,
            YieldRuleCode = ruleCode,
            YieldRuleLabel = ruleLabel,
            YieldAdjustmentPercent = adjustmentPercent
        };
    }
}
