using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Une valeur d'indicateur, telle que le moteur vient de la calculer, pour un perimetre donne.
/// C'est un resultat BRUT : ni comparaison N-1, ni budget, ni verdict de seuil - tout cela est
/// ajoute ensuite par le service, a partir de deux mesures et des seuils configures.
///
/// <see cref="Numerator"/> et <see cref="Denominator"/> sont conserves a cote de la valeur pour
/// deux raisons qui comptent : ils permettent de consolider un groupe correctement (somme des
/// numerateurs / somme des denominateurs, voir <see cref="KpiAggregation.RatioOfSums"/>) sans
/// jamais moyenner des taux, et ils rendent le chiffre verifiable a la main par celui qui le lit.
///
/// <see cref="MissingData"/> n'est pas decoratif : c'est ce qui distingue "l'indicateur vaut
/// zero" de "je ne sais pas", et c'est la seule chose qui permette a un ecran de dire a
/// l'utilisateur QUOI corriger pour obtenir un chiffre.
/// </summary>
public sealed record KpiMeasure(
    string Code,
    string? HotelUnitCode,
    decimal? Value,
    decimal? Numerator,
    decimal? Denominator,
    KpiQuality Quality,
    IReadOnlyCollection<string> MissingData)
{
    /// <summary>Une mesure valide, construite a partir d'un rapport deja calcule.</summary>
    public static KpiMeasure Ratio(
        string code,
        string? hotelUnitCode,
        decimal numerator,
        decimal denominator,
        Func<decimal, decimal, decimal?> divide,
        string missingDenominatorReason)
    {
        var value = divide(numerator, denominator);

        return new KpiMeasure(
            code,
            hotelUnitCode,
            value,
            KpiMath.Round(numerator),
            KpiMath.Round(denominator),
            value is null ? KpiQuality.MissingData : KpiQuality.Valid,
            value is null ? [missingDenominatorReason] : []);
    }

    /// <summary>Une mesure additive : la valeur EST le numerateur, il n'y a pas de denominateur.</summary>
    public static KpiMeasure Amount(string code, string? hotelUnitCode, decimal value)
    {
        return new KpiMeasure(
            code,
            hotelUnitCode,
            KpiMath.Round(value),
            KpiMath.Round(value),
            null,
            KpiQuality.Valid,
            []);
    }

    /// <summary>
    /// Une mesure qui n'a pas lieu d'exister sur ce perimetre : indicateur en attente de sa
    /// source, ou indicateur groupe demande par unite. La valeur est nulle et la raison est
    /// portee, jamais un zero muet.
    /// </summary>
    public static KpiMeasure NotApplicable(string code, string? hotelUnitCode, string reason)
    {
        return new KpiMeasure(code, hotelUnitCode, null, null, null, KpiQuality.NotApplicable, [reason]);
    }

    /// <summary>Une mesure impossible faute d'une donnee indispensable.</summary>
    public static KpiMeasure Missing(string code, string? hotelUnitCode, params string[] reasons)
    {
        return new KpiMeasure(code, hotelUnitCode, null, null, null, KpiQuality.MissingData, reasons);
    }

    /// <summary>
    /// La meme mesure, degradee en <see cref="KpiQuality.Partial"/> avec la raison ajoutee -
    /// utilisee quand la valeur est calculable mais qu'une partie du perimetre manque. Une
    /// mesure deja indisponible n'est pas "amelioree" en partielle au passage.
    /// </summary>
    public KpiMeasure WithWarning(string reason)
    {
        if (Quality is KpiQuality.MissingData or KpiQuality.NotApplicable)
        {
            return this with { MissingData = [.. MissingData, reason] };
        }

        return this with
        {
            Quality = KpiQuality.Partial,
            MissingData = [.. MissingData, reason]
        };
    }
}
