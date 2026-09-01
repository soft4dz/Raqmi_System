using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Assemble une mesure brute, ses trois references et ses bornes en un indicateur pret a
/// afficher : ecarts, tendance et verdict de sante.
///
/// Pure et sans etat, comme les calculateurs : c'est ici que se decide ce que l'utilisateur lit
/// comme "favorable" ou "critique", et cette decision doit etre testable sans base ni HTTP.
///
/// LE BUDGET NE CONCERNE QUE LE CHIFFRE D'AFFAIRES. Le module Budget de Raqmi System budgete des
/// recettes, ventilees en hebergement, restauration, boissons et autres - il ne budgete ni un
/// taux d'occupation, ni un food cost, ni une masse salariale. Les colonnes budget des autres
/// indicateurs restent donc vides, et c'est la verite : y afficher un zero laisserait croire a
/// un objectif de zero. Pour ces indicateurs, la reference de pilotage est l'OBJECTIF, saisi
/// avec les seuils (<see cref="KpiThreshold.TargetValue"/>).
/// </summary>
public static class KpiMeasureAssembler
{
    public static KpiMeasureResponse Assemble(
        KpiDefinition definition,
        KpiMeasure measure,
        string? hotelUnitName,
        KpiMeasure? previous,
        decimal? budgetValue,
        KpiThresholdSet thresholds,
        KpiSnapshot? snapshot)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(measure);
        ArgumentNullException.ThrowIfNull(thresholds);

        var previousValue = previous?.Value;
        var health = KpiMath.Classify(
            measure.Value,
            thresholds.FavorableThreshold,
            thresholds.CriticalThreshold,
            definition.Polarity);

        var missing = measure.MissingData;

        // Un instantane CLOTURE qui ne dit pas la meme chose que le recalcul n'est jamais
        // corrige en silence : la divergence est signalee, et les deux valeurs sont exposees
        // cote a cote pour que le lecteur tranche. C'est la meme discipline qu'une ecriture
        // comptabilisee, qui se corrige par une extourne et jamais par une modification.
        if (snapshot is not null && snapshot.IsClosed && snapshot.DivergesFrom(measure.Value))
        {
            missing =
            [
                .. missing,
                $"La valeur figee a la cloture ({Format(snapshot.Value)}) differe du recalcul "
                + $"({Format(measure.Value)}) : des donnees de la periode ont ete modifiees apres la cloture."
            ];
        }

        return new KpiMeasureResponse(
            definition.Code,
            definition.Name,
            definition.ShortName,
            definition.Category,
            definition.Description,
            definition.Formula,
            definition.Unit,
            definition.Polarity,
            definition.Aggregation,
            definition.ScopeLevel,
            definition.Availability,
            definition.SourceModule,
            definition.SourceDetail,
            definition.FormulaVersion,
            measure.HotelUnitCode,
            hotelUnitName,
            measure.Value,
            measure.Numerator,
            measure.Denominator,
            measure.Quality,
            missing,
            previousValue,
            KpiMath.Difference(previousValue, measure.Value),
            KpiMath.Variation(previousValue, measure.Value),
            budgetValue,
            KpiMath.Difference(budgetValue, measure.Value),
            KpiMath.Variation(budgetValue, measure.Value),
            thresholds.TargetValue,
            KpiMath.Difference(thresholds.TargetValue, measure.Value),
            KpiMath.Variation(thresholds.TargetValue, measure.Value),
            KpiMath.Trend(previousValue, measure.Value),
            health,
            thresholds.FavorableThreshold,
            thresholds.CriticalThreshold,
            thresholds.OwnerRole,
            snapshot?.Status,
            snapshot?.Value);
    }

    /// <summary>
    /// L'alerte que cet indicateur declenche, ou null s'il n'en declenche aucune. Seuls les
    /// verdicts <see cref="KpiHealth.Watch"/> et <see cref="KpiHealth.Critical"/> alertent :
    /// "favorable" ne demande rien, et "inconnu" - faute de seuil ou de valeur - ne peut rien
    /// affirmer. Une absence de seuil n'est pas un satisfecit, mais elle n'est pas non plus une
    /// alerte : c'est un parametrage a faire, que le tableau de bord signale ailleurs.
    /// </summary>
    public static KpiAlertResponse? ToAlert(
        KpiMeasureResponse measure,
        KpiPeriod period,
        DateTimeOffset evaluatedAt)
    {
        ArgumentNullException.ThrowIfNull(measure);
        ArgumentNullException.ThrowIfNull(period);

        if (measure.Health is not (KpiHealth.Watch or KpiHealth.Critical) || measure.Value is null)
        {
            return null;
        }

        var severity = measure.Health == KpiHealth.Critical
            ? KpiAlertSeverity.Critical
            : KpiAlertSeverity.Watch;

        var breached = severity == KpiAlertSeverity.Critical
            ? measure.CriticalThreshold
            : measure.FavorableThreshold;

        var direction = measure.Polarity == KpiPolarity.LowerIsBetter ? "au-dessus de" : "en deca de";
        var scope = measure.HotelUnitName ?? measure.HotelUnitCode ?? "Groupe";

        var message = severity == KpiAlertSeverity.Critical
            ? $"{measure.Name} ({scope}) : {Format(measure.Value)}, {direction} la borne critique {Format(breached)}."
            : $"{measure.Name} ({scope}) : {Format(measure.Value)}, {direction} la borne favorable {Format(breached)}.";

        return new KpiAlertResponse(
            measure.Code,
            measure.Name,
            measure.Category,
            measure.HotelUnitCode,
            measure.HotelUnitName,
            measure.Value,
            measure.Unit,
            breached,
            severity,
            measure.OwnerRole,
            period.From,
            period.To,
            evaluatedAt,
            message);
    }

    private static string Format(decimal? value)
    {
        return value is null
            ? "-"
            : value.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }
}
