namespace RaqmiSystem.Domain.Kpi;

/// <summary>
/// L'arithmetique commune a TOUS les indicateurs : division, pourcentage, variation, tendance
/// et verdict de seuil. Une seule implementation, dans le domaine, pour que le taux d'occupation
/// et le food cost ne puissent pas diverger sur la facon de traiter un denominateur nul ou un
/// arrondi.
///
/// LA REGLE DE LA DIVISION PAR ZERO, valable partout dans le moteur : un rapport dont le
/// denominateur est nul n'existe pas, il ne vaut pas zero. Un RevPAR sans chambre disponible,
/// un food cost sans CA restauration, une variation contre un N-1 vide : tous renvoient null,
/// que l'ecran affiche par un tiret. Renvoyer 0 dirait "l'hotel n'a rien produit" la ou la
/// verite est "la question ne se pose pas" - c'est la meme regle que
/// <c>BudgetVarianceCalculator.Percentage</c> et <c>GroupDashboardCalculator</c>, reprise ici
/// telle quelle plutot que redecidee.
///
/// L'ARRONDI est toujours a deux decimales, MidpointRounding.AwayFromZero : la convention
/// monetaire du depot, appliquee aussi aux taux pour qu'un pourcentage affiche et un
/// pourcentage recalcule a la main coincident.
/// </summary>
public static class KpiMath
{
    public const int Scale = 2;

    /// <summary>
    /// Ecart relatif, en points de pourcentage, en dessous duquel deux valeurs sont declarees
    /// stables. Sans cette bande, un CA qui bouge de 0,01 % afficherait une fleche de tendance
    /// et ferait croire a un mouvement.
    /// </summary>
    public const decimal FlatTrendTolerancePercent = 0.5m;

    public static decimal Round(decimal value)
    {
        return Math.Round(value, Scale, MidpointRounding.AwayFromZero);
    }

    public static decimal? Round(decimal? value)
    {
        return value is null ? null : Round(value.Value);
    }

    /// <summary>
    /// Quotient brut arrondi, ou null quand le denominateur est nul. Sert aux indicateurs dont
    /// l'unite est celle du numerateur (ADR, RevPAR, cout par chambre, duree moyenne).
    /// </summary>
    public static decimal? Divide(decimal numerator, decimal denominator)
    {
        if (denominator == 0m)
        {
            return null;
        }

        return Round(numerator / denominator);
    }

    /// <summary>
    /// Quotient exprime en pourcentage (0-100 et au-dela), ou null quand le denominateur est
    /// nul. Sert a tous les taux : occupation, food cost, masse salariale sur CA, absenteisme.
    /// </summary>
    public static decimal? Percent(decimal numerator, decimal denominator)
    {
        if (denominator == 0m)
        {
            return null;
        }

        return Round(numerator * 100m / denominator);
    }

    /// <summary>
    /// Ecart relatif en pourcentage entre une valeur et sa reference (N-1, budget, objectif).
    /// Null quand la reference est nulle ou absente : on ne mesure pas une progression contre
    /// rien.
    /// </summary>
    public static decimal? Variation(decimal? reference, decimal? value)
    {
        if (reference is null || value is null || reference.Value == 0m)
        {
            return null;
        }

        return Round((value.Value - reference.Value) / Math.Abs(reference.Value) * 100m);
    }

    /// <summary>Ecart absolu, ou null des qu'un des deux termes manque.</summary>
    public static decimal? Difference(decimal? reference, decimal? value)
    {
        if (reference is null || value is null)
        {
            return null;
        }

        return Round(value.Value - reference.Value);
    }

    /// <summary>
    /// Sens d'evolution par rapport a la reference. Purement arithmetique : la polarite de
    /// l'indicateur n'intervient pas ici, une hausse est une hausse meme quand elle est une
    /// mauvaise nouvelle.
    ///
    /// Cas particulier volontaire : quand la reference est nulle ET la valeur non nulle, la
    /// variation en pourcentage n'existe pas (division par zero) mais la TENDANCE, elle, existe
    /// - passer de 0 a 10 est une hausse. C'est pour cela que la tendance ne se lit pas sur la
    /// variation mais sur les deux valeurs.
    /// </summary>
    public static KpiTrend Trend(decimal? reference, decimal? value)
    {
        if (reference is null || value is null)
        {
            return KpiTrend.Unknown;
        }

        if (reference.Value == 0m)
        {
            return value.Value == 0m ? KpiTrend.Flat
                : value.Value > 0m ? KpiTrend.Up
                : KpiTrend.Down;
        }

        var variation = (value.Value - reference.Value) / Math.Abs(reference.Value) * 100m;

        if (Math.Abs(variation) < FlatTrendTolerancePercent)
        {
            return KpiTrend.Flat;
        }

        return variation > 0m ? KpiTrend.Up : KpiTrend.Down;
    }

    /// <summary>
    /// Verdict d'une valeur face a ses deux bornes, lues dans le sens de la polarite de
    /// l'indicateur.
    ///
    /// Pour un indicateur ou la hausse est bonne : favorable des que la valeur atteint la borne
    /// favorable, critique des qu'elle tombe a la borne critique ou en dessous, vigilance
    /// entre les deux. Pour un indicateur ou la baisse est bonne, les deux comparaisons
    /// s'inversent. Les bornes sont INCLUSIVES : un food cost exactement egal au seuil critique
    /// est critique - un seuil qu'on peut atteindre sans consequence n'est pas un seuil.
    ///
    /// Une borne absente est simplement ignoree ; aucune borne du tout, ou pas de valeur, donne
    /// <see cref="KpiHealth.Unknown"/> et non "favorable" - l'absence de seuil n'est pas un
    /// satisfecit.
    /// </summary>
    public static KpiHealth Classify(
        decimal? value,
        decimal? favorableThreshold,
        decimal? criticalThreshold,
        KpiPolarity polarity)
    {
        if (value is null || polarity == KpiPolarity.Neutral)
        {
            return KpiHealth.Unknown;
        }

        if (favorableThreshold is null && criticalThreshold is null)
        {
            return KpiHealth.Unknown;
        }

        var current = value.Value;

        if (polarity == KpiPolarity.HigherIsBetter)
        {
            if (favorableThreshold is not null && current >= favorableThreshold.Value)
            {
                return KpiHealth.Favorable;
            }

            if (criticalThreshold is not null && current <= criticalThreshold.Value)
            {
                return KpiHealth.Critical;
            }

            return KpiHealth.Watch;
        }

        if (favorableThreshold is not null && current <= favorableThreshold.Value)
        {
            return KpiHealth.Favorable;
        }

        if (criticalThreshold is not null && current >= criticalThreshold.Value)
        {
            return KpiHealth.Critical;
        }

        return KpiHealth.Watch;
    }

    /// <summary>
    /// La periode equivalente un an plus tot, definition unique partagee par le service (qui
    /// va chercher les faits) et le moteur (qui rapporte les bornes).
    /// <c>DateOnly.AddYears</c> ramene le 29 fevrier au 28 les annees non bissextiles.
    /// </summary>
    public static (DateOnly From, DateOnly To) PreviousYearPeriod(DateOnly from, DateOnly to)
    {
        return (from.AddYears(-1), to.AddYears(-1));
    }
}
