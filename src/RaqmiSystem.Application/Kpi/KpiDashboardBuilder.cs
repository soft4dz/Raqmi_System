using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Construit les reponses de la bibliotheque - tableau de bord, comparatif, alertes - a partir
/// de deux passes de calcul, des seuils configures, des instantanes deja poses et des droits du
/// profil connecte.
///
/// Pure et sans acces base, comme les calculateurs : ce qui decide de la mise en page d'un
/// tableau de bord de direction, de l'ordre des classements et de ce qui est masque par les
/// permissions se teste sans base ni HTTP.
///
/// LE FILTRE DE PERMISSIONS EST APPLIQUE ICI, donc du cote serveur, avant que la moindre valeur
/// ne parte : un indicateur que le profil n'a pas le droit de lire est retire de la reponse et
/// compte dans <see cref="KpiDashboardResponse.HiddenByPermission"/>. Passer par l'API plutot
/// que par l'ecran ne change rien, et le tableau de bord dit combien de lignes il ne montre pas
/// - un ecran qui perd des lignes sans le dire fait douter de tous les autres chiffres.
/// </summary>
public sealed class KpiDashboardBuilder
{
    private static readonly IReadOnlyDictionary<KpiCategory, string> CategoryLabels =
        new Dictionary<KpiCategory, string>
        {
            [KpiCategory.Accommodation] = "Hebergement",
            [KpiCategory.Finance] = "Finance",
            [KpiCategory.FoodBeverage] = "Restauration et boissons",
            [KpiCategory.HumanResources] = "Ressources humaines",
            [KpiCategory.Maintenance] = "Maintenance",
            [KpiCategory.GuestExperience] = "Experience client",
            [KpiCategory.SupplyChain] = "Achats et stocks"
        };

    public KpiDashboardResponse BuildDashboard(
        KpiQuery query,
        KpiComputation current,
        KpiComputation previous,
        IReadOnlyCollection<KpiUnitFact> units,
        IReadOnlyCollection<KpiThreshold> thresholds,
        IReadOnlyCollection<KpiSnapshot> snapshots,
        KpiAccessContext access,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(access);

        var period = current.Period;
        var previousPeriod = previous.Period;
        var scope = query.HotelUnitCode;

        var definitions = VisibleDefinitions(query, access, out var hidden);

        var byScope = definitions
            .Select(definition => Assemble(definition, scope, current, previous, thresholds, snapshots, units))
            .ToArray();

        var headline = KpiCatalog.DirectionHeadlineCodes
            .Select(code => byScope.FirstOrDefault(measure => measure.Code == code))
            .Where(measure => measure is not null)
            .Select(measure => measure!)
            .ToArray();

        var sections = byScope
            .GroupBy(measure => measure.Category)
            .OrderBy(group => group.Key)
            .Select(group => new KpiCategorySection(
                group.Key,
                CategoryLabels.GetValueOrDefault(group.Key, group.Key.ToString()),
                group.ToArray()))
            .ToArray();

        // Le premier niveau de descente : les indicateurs de tete, unite par unite. Une seule
        // unite demandee, pas de descente - on y est deja.
        var unitCards = scope is not null
            ? Array.Empty<KpiUnitCard>()
            : units
                .OrderBy(unit => unit.Code, StringComparer.Ordinal)
                .Select(unit => new KpiUnitCard(
                    unit.Code,
                    unit.Name,
                    unit.IsActive,
                    KpiCatalog.DirectionHeadlineCodes
                        .Select(KpiCatalog.Find)
                        .Where(definition => definition is not null && access.CanRead(definition))
                        .Select(definition => Assemble(
                            definition!, unit.Code, current, previous, thresholds, snapshots, units))
                        .ToArray()))
                .ToArray();

        var alerts = BuildAlerts(byScope, unitCards, period, now);

        return new KpiDashboardResponse(
            period.From,
            period.To,
            period.Granularity,
            previousPeriod.From,
            previousPeriod.To,
            scope,
            query.DsoMethod,
            headline,
            sections,
            unitCards,
            alerts,
            hidden,
            now,
            BuildBasis(query));
    }

    public KpiComparisonResponse BuildComparison(
        KpiQuery query,
        KpiComputation current,
        KpiComputation previous,
        IReadOnlyCollection<KpiUnitFact> units,
        IReadOnlyCollection<KpiThreshold> thresholds,
        IReadOnlyCollection<KpiSnapshot> snapshots,
        KpiAccessContext access,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(access);

        var definitions = KpiCatalog.BenchmarkCodes
            .Select(KpiCatalog.Find)
            .Where(definition => definition is not null && access.CanRead(definition))
            .Select(definition => definition!)
            .ToArray();

        KpiComparisonRow BuildRow(string? unitCode, string name, bool isActive) => new(
            unitCode,
            name,
            isActive,
            definitions
                .Select(definition => Assemble(
                    definition, unitCode, current, previous, thresholds, snapshots, units))
                .ToArray());

        var rows = new List<KpiComparisonRow> { BuildRow(null, "Groupe", true) };

        rows.AddRange(units
            .OrderBy(unit => unit.Code, StringComparer.Ordinal)
            .Select(unit => BuildRow(unit.Code, unit.Name, unit.IsActive)));

        return new KpiComparisonResponse(
            current.Period.From,
            current.Period.To,
            current.Period.Granularity,
            previous.Period.From,
            previous.Period.To,
            definitions.Select(definition => definition.Code).ToArray(),
            rows,
            BuildRankings(definitions, rows.Where(row => row.HotelUnitCode is not null).ToArray()),
            now,
            BuildBasis(query));
    }

    /// <summary>
    /// Les classements, indicateur par indicateur et jamais fondus en une note globale : un
    /// score composite suppose des ponderations que la direction seule peut fixer, et aucune
    /// ponderation par defaut ne serait defendable.
    ///
    /// Les indicateurs sans valeur ne participent a aucun classement : une unite dont le food
    /// cost est indisponible n'est ni la meilleure ni la pire, elle n'est pas comparable.
    /// </summary>
    private static IReadOnlyCollection<KpiRanking> BuildRankings(
        IReadOnlyCollection<KpiDefinition> definitions,
        IReadOnlyCollection<KpiComparisonRow> unitRows)
    {
        var rankings = new List<KpiRanking>();

        foreach (var definition in definitions.Where(definition => definition.Polarity != KpiPolarity.Neutral))
        {
            var measured = unitRows
                .Select(row => (Row: row, Measure: row.Measures.FirstOrDefault(m => m.Code == definition.Code)))
                .Where(pair => pair.Measure?.Value is not null)
                .ToArray();

            if (measured.Length < 2)
            {
                // Un classement d'une seule unite n'apprend rien a personne.
                continue;
            }

            var higherIsBetter = definition.Polarity == KpiPolarity.HigherIsBetter;

            var best = higherIsBetter
                ? measured.MaxBy(pair => pair.Measure!.Value!.Value)
                : measured.MinBy(pair => pair.Measure!.Value!.Value);

            var worst = higherIsBetter
                ? measured.MinBy(pair => pair.Measure!.Value!.Value)
                : measured.MaxBy(pair => pair.Measure!.Value!.Value);

            rankings.Add(new KpiRanking(
                KpiRankingKind.BestPerformance,
                definition.Code,
                definition.Name,
                best.Row.HotelUnitCode!,
                best.Row.HotelUnitName,
                best.Measure!.Value,
                null,
                $"Meilleure valeur du groupe sur {definition.Name}."));

            rankings.Add(new KpiRanking(
                KpiRankingKind.WeakestPerformance,
                definition.Code,
                definition.Name,
                worst.Row.HotelUnitCode!,
                worst.Row.HotelUnitName,
                worst.Measure!.Value,
                null,
                $"Valeur la plus faible du groupe sur {definition.Name}."));

            // Progression : la variation N-1 lue DANS LE SENS de l'indicateur. Un food cost qui
            // baisse de trois points est une progression, meme si le nombre a diminue.
            var progressed = measured
                .Where(pair => pair.Measure!.PreviousVariancePercent is not null)
                .ToArray();

            if (progressed.Length >= 2)
            {
                var strongest = higherIsBetter
                    ? progressed.MaxBy(pair => pair.Measure!.PreviousVariancePercent!.Value)
                    : progressed.MinBy(pair => pair.Measure!.PreviousVariancePercent!.Value);

                rankings.Add(new KpiRanking(
                    KpiRankingKind.StrongestProgress,
                    definition.Code,
                    definition.Name,
                    strongest.Row.HotelUnitCode!,
                    strongest.Row.HotelUnitName,
                    strongest.Measure!.Value,
                    strongest.Measure.PreviousVariancePercent,
                    $"Plus forte progression sur {definition.Name} par rapport a la periode equivalente un an plus tot."));
            }

            // Ecart budget : en valeur ABSOLUE. Un depassement et un manque de meme ampleur
            // meritent tous deux l'attention de la direction ; ne montrer que les manques ferait
            // passer un budget largement depasse pour une bonne nouvelle sans discussion.
            var budgeted = measured
                .Where(pair => pair.Measure!.BudgetVarianceAmount is not null)
                .ToArray();

            if (budgeted.Length >= 2)
            {
                var largest = budgeted.MaxBy(pair => Math.Abs(pair.Measure!.BudgetVarianceAmount!.Value));

                rankings.Add(new KpiRanking(
                    KpiRankingKind.LargestBudgetGap,
                    definition.Code,
                    definition.Name,
                    largest.Row.HotelUnitCode!,
                    largest.Row.HotelUnitName,
                    largest.Measure!.Value,
                    largest.Measure.BudgetVarianceAmount,
                    $"Plus fort ecart au budget sur {definition.Name}, a la hausse comme a la baisse."));
            }
        }

        return rankings;
    }

    /// <summary>
    /// Les alertes du perimetre analyse et de chaque unite, les plus graves d'abord. L'ordre est
    /// stable a donnees egales - gravite, puis unite, puis code - pour qu'un rafraichissement ne
    /// fasse pas danser la liste sous les yeux du lecteur.
    /// </summary>
    public IReadOnlyCollection<KpiAlertResponse> BuildAlerts(
        IReadOnlyCollection<KpiMeasureResponse> scopeMeasures,
        IReadOnlyCollection<KpiUnitCard> unitCards,
        KpiPeriod period,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(scopeMeasures);
        ArgumentNullException.ThrowIfNull(unitCards);

        var alerts = scopeMeasures
            .Select(measure => KpiMeasureAssembler.ToAlert(measure, period, now))
            .Concat(unitCards
                .SelectMany(card => card.Headline)
                .Select(measure => KpiMeasureAssembler.ToAlert(measure, period, now)))
            .Where(alert => alert is not null)
            .Select(alert => alert!)
            .ToArray();

        return alerts
            .OrderByDescending(alert => alert.Severity)
            .ThenBy(alert => alert.HotelUnitCode ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(alert => alert.KpiCode, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Les indicateurs que ce profil peut lire, dans l'ordre du catalogue. Un departement
    /// demande restreint la reponse a la famille RH, seule famille dont la donnee porte
    /// reellement un departement.
    /// </summary>
    private static KpiDefinition[] VisibleDefinitions(
        KpiQuery query,
        KpiAccessContext access,
        out int hiddenByPermission)
    {
        var candidates = KpiCatalog.All
            .Where(definition => query.DepartmentCode is null
                || definition.Category == KpiCategory.HumanResources)
            .ToArray();

        var visible = candidates.Where(access.CanRead).ToArray();
        hiddenByPermission = candidates.Length - visible.Length;

        return visible;
    }

    private static KpiMeasureResponse Assemble(
        KpiDefinition definition,
        string? unitCode,
        KpiComputation current,
        KpiComputation previous,
        IReadOnlyCollection<KpiThreshold> thresholds,
        IReadOnlyCollection<KpiSnapshot> snapshots,
        IReadOnlyCollection<KpiUnitFact> units)
    {
        var measure = current.Require(definition.Code, unitCode);

        var snapshot = snapshots.FirstOrDefault(candidate =>
            string.Equals(candidate.KpiCode, definition.Code, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.HotelUnitCode, unitCode, StringComparison.OrdinalIgnoreCase)
            && candidate.PeriodStart == current.Period.From
            && candidate.PeriodEnd == current.Period.To);

        return KpiMeasureAssembler.Assemble(
            definition,
            measure,
            units.FirstOrDefault(unit =>
                string.Equals(unit.Code, unitCode, StringComparison.OrdinalIgnoreCase))?.Name,
            previous.Find(definition.Code, unitCode),
            ResolveBudget(definition, measure, current, unitCode),
            KpiThresholdSet.Resolve(definition.Code, unitCode, thresholds),
            snapshot);
    }

    /// <summary>
    /// La reference budgetaire d'un indicateur.
    ///
    /// Le module Budget de Raqmi System budgete des RECETTES et rien d'autre : ni taux
    /// d'occupation, ni food cost, ni masse salariale. Seul le chiffre d'affaires a donc une
    /// colonne budget, et elle est deduite de l'ecart deja calcule (objectif = realise - ecart)
    /// plutot que resommee ici - une seule addition des lignes budgetaires dans tout le moteur,
    /// donc aucune chance que la colonne budget et l'ecart budgetaire se contredisent.
    ///
    /// Pour tous les autres indicateurs la colonne reste vide, et c'est la verite : y afficher un
    /// zero laisserait croire a un objectif de zero. Leur reference de pilotage est l'OBJECTIF,
    /// saisi avec les seuils.
    /// </summary>
    private static decimal? ResolveBudget(
        KpiDefinition definition,
        KpiMeasure measure,
        KpiComputation current,
        string? unitCode)
    {
        if (definition.Code != KpiCodes.RevenueTotal || measure.Value is null)
        {
            return null;
        }

        var variance = current.Find(KpiCodes.RevenueBudgetVariance, unitCode)?.Value;

        return variance is null ? null : measure.Value.Value - variance.Value;
    }

    private static KpiBasis BuildBasis(KpiQuery query)
    {
        return new KpiBasis(
            "Seules les recettes journalieres au statut Validee sont comptees ; brouillons, "
            + "recettes soumises et rejetees n'en font pas partie.",
            "Une nuitee est occupee quand un sejour tenant l'inventaire couvre la chambre cette "
            + "nuit-la, gratuites comprises. Les nuitees disponibles sont les chambres actives "
            + "moins les chambres retirees de la vente, nuit par nuit. L'ADR, lui, divise par les "
            + "nuitees VENDUES : les gratuites en sont exclues.",
            "Seules les factures au statut Emise, datees au plus tard a la fin de periode, sont "
            + "dues ; leur age court depuis la date de facture, faute d'echeance dans le systeme.",
            "Seuls les bulletins de paie valides comptent, et le cout retenu est le cout employeur "
            + "complet imprime sur le bulletin. Un mois de paie que la periode touche compte en "
            + "entier.",
            "Le cout matiere est une SORTIE de stock valorisee, pas un achat : seuls les "
            + "mouvements de consommation comptent. Une sortie sans cout unitaire est signalee "
            + "comme donnee manquante, jamais comptee pour zero.",
            "Le resultat d'exploitation est reconstruit sur les ecritures comptabilisees, selon le "
            + "rattachement de comptes configure par l'etablissement. Sans ce rattachement, GOP, "
            + "EBE et marges repondent \"donnee manquante\" plutot que d'afficher le resultat "
            + "comptable complet sous le nom de GOP.",
            query.CompareToBudget
                ? "L'objectif d'une periode est la somme des objectifs MENSUELS des plans "
                    + "budgetaires figes que la periode touche, un mois partiellement couvert "
                    + "comptant en entier. Le module Budget ne budgete que des recettes : les "
                    + "autres indicateurs n'ont pas de colonne budget, seulement un objectif "
                    + "saisi avec leurs seuils."
                : "Comparaison au budget desactivee pour cette requete.",
            "Un taux ne se moyenne jamais entre unites : la valeur du groupe est calculee "
            + "directement sur les faits du groupe, ce qui equivaut a la somme des numerateurs "
            + "divisee par la somme des denominateurs. Les indicateurs issus de la comptabilite "
            + "et des ordres de paiement n'existent qu'au niveau groupe : ces donnees ne portent "
            + "pas d'unite hoteliere.",
            "Un indicateur sans valeur affiche un tiret et la raison, jamais un zero. Zero "
            + "signifie \"mesure et nul\" ; l'absence de valeur signifie \"la question ne se pose "
            + "pas\" ou \"il manque une donnee\", et les deux sont distingues.");
    }
}
