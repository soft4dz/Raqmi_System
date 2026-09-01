using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Le moteur : il enchaine les calculateurs par famille, une fois pour le groupe et une fois par
/// unite, et garantit qu'AUCUN indicateur du catalogue ne reste sans reponse.
///
/// CALCUL DIRECT PAR PERIMETRE, PAS CONSOLIDATION. Le chiffre du groupe est calcule sur les
/// faits du groupe, pas reconstitue a partir des chiffres par unite. C'est plus simple, plus sur
/// - aucune question de mise a l'echelle d'un numerateur - et cela rend la regle d'agregation du
/// catalogue verifiable plutot qu'implicite : le test
/// <c>Group_ratio_equals_ratio_of_sums_over_units</c> prouve que le calcul direct du groupe
/// coincide avec la somme des numerateurs divisee par la somme des denominateurs, ce qui est
/// exactement la garantie annoncee par <see cref="KpiAggregation.RatioOfSums"/>.
///
/// AUCUN TROU. Pour chaque indicateur du catalogue et chaque perimetre demande, une mesure est
/// produite. Un indicateur en attente de sa source rend une mesure "sans objet" portant le nom
/// de ce qui lui manque ; un indicateur qui n'existe qu'au niveau groupe rend, par unite, une
/// mesure "sans objet" expliquant pourquoi. C'est ce qui permet a un ecran d'afficher la
/// bibliotheque entiere sans jamais laisser une case muette.
///
/// AUCUN ACCES BASE. Le moteur ne connait que des faits deja rapatries : toutes ses regles sont
/// donc testables sans base de donnees, et le service EF qui l'alimente ne peut pas devenir par
/// accident le lieu ou vit une definition d'indicateur.
/// </summary>
public sealed class KpiEngine
{
    private readonly LodgingKpiCalculator lodging = new();
    private readonly FinanceKpiCalculator finance = new();
    private readonly FoodBeverageKpiCalculator foodBeverage = new();
    private readonly WorkforceKpiCalculator workforce = new();
    private readonly GuestExperienceKpiCalculator guestExperience = new();

    private const string GroupOnlyReason =
        "Indicateur disponible au niveau groupe uniquement : sa donnee source ne porte pas "
        + "d'unite hoteliere dans Raqmi System.";

    public KpiComputation Compute(
        KpiPeriod period,
        KpiFactSet facts,
        DateOnly today,
        KpiDsoMethod dsoMethod = KpiDsoMethod.Simple)
    {
        ArgumentNullException.ThrowIfNull(period);
        ArgumentNullException.ThrowIfNull(facts);

        var measures = new List<KpiMeasure>();

        var groupCapacity = ComputeScope(period, null, facts, today, dsoMethod, measures);

        foreach (var unit in facts.Units.OrderBy(unit => unit.Code, StringComparer.Ordinal))
        {
            ComputeScope(period, unit.Code, ScopeTo(facts, unit.Code), today, dsoMethod, measures);
        }

        return new KpiComputation(period, measures, groupCapacity);
    }

    /// <summary>
    /// Toutes les mesures d'un perimetre : celles que les calculateurs produisent, puis les
    /// mesures "sans objet" de tout ce que le catalogue declare et que ce perimetre ne peut pas
    /// rendre. Renvoie la capacite du perimetre, que l'appelant conserve pour le groupe.
    ///
    /// L'hebergement est calcule EN PREMIER et sa capacite est pretee aux autres familles : le
    /// GOPPAR, le CPOR et les couts salariaux par chambre divisent tous par les memes nuitees
    /// disponibles, et deux implementations de ce denominateur dans le meme produit finiraient
    /// par diverger d'une chambre en travaux.
    /// </summary>
    private KpiCapacity ComputeScope(
        KpiPeriod period,
        string? unitCode,
        KpiFactSet facts,
        DateOnly today,
        KpiDsoMethod dsoMethod,
        List<KpiMeasure> sink)
    {
        var produced = new List<KpiMeasure>();

        produced.AddRange(lodging.Compute(period, unitCode, facts));

        var capacity = ReadCapacity(produced);

        produced.AddRange(finance.Compute(period, unitCode, facts, capacity, today, dsoMethod));
        produced.AddRange(foodBeverage.Compute(period, unitCode, facts));
        produced.AddRange(workforce.Compute(period, unitCode, facts, capacity));
        produced.AddRange(guestExperience.Compute(period, unitCode, facts));

        var computed = produced.Select(measure => measure.Code).ToHashSet(StringComparer.Ordinal);

        foreach (var definition in KpiCatalog.All)
        {
            if (computed.Contains(definition.Code))
            {
                continue;
            }

            produced.Add(KpiMeasure.NotApplicable(
                definition.Code,
                unitCode,
                Explain(definition, unitCode)));
        }

        sink.AddRange(produced);

        return capacity;
    }

    /// <summary>
    /// Pourquoi cet indicateur n'a pas de valeur ici. L'ordre des tests compte : un indicateur en
    /// attente de sa source n'a de valeur nulle part, la maille ne vient qu'ensuite.
    /// </summary>
    private static string Explain(KpiDefinition definition, string? unitCode)
    {
        if (definition.Availability == KpiAvailability.AwaitingSource)
        {
            return definition.MissingSource
                ?? "La donnee source de cet indicateur n'existe pas encore dans Raqmi System.";
        }

        if (definition.ScopeLevel == KpiScopeLevel.GroupOnly && unitCode is not null)
        {
            return GroupOnlyReason;
        }

        return "Cet indicateur n'a pas ete calcule sur ce perimetre.";
    }

    /// <summary>
    /// Relit la capacite sur les mesures d'hebergement deja produites, plutot que de la
    /// recalculer : la capacite affichee et la capacite utilisee comme denominateur sont ainsi
    /// le meme nombre, par construction.
    /// </summary>
    private static KpiCapacity ReadCapacity(IReadOnlyCollection<KpiMeasure> lodgingMeasures)
    {
        var available = lodgingMeasures
            .FirstOrDefault(measure => measure.Code == KpiCodes.RoomsAvailable)?.Value ?? 0m;

        var occupied = lodgingMeasures
            .FirstOrDefault(measure => measure.Code == KpiCodes.RoomsOccupied)?.Value ?? 0m;

        return new KpiCapacity((int)available, (int)occupied);
    }

    /// <summary>
    /// Restreint les faits a une seule unite.
    ///
    /// Deux collections restent VIDES par construction : les ordres de paiement et les lignes
    /// d'ecriture. Elles ne portent pas d'unite hoteliere, et les repartir au prorata d'une cle
    /// quelconque fabriquerait une comptabilite analytique que l'etablissement n'a pas mise en
    /// place. Les indicateurs qui en dependent sont declares <see cref="KpiScopeLevel.GroupOnly"/>
    /// et rendent, par unite, une mesure sans objet qui le dit.
    ///
    /// Deux autres restent COMPLETES : les articles de stock et les regles de rattachement de
    /// comptes sont des referentiels, pas des transactions - les restreindre priverait le
    /// calculateur de la famille d'un article ou du sens d'un compte.
    /// </summary>
    private static KpiFactSet ScopeTo(KpiFactSet facts, string unitCode)
    {
        bool Matches(string code) => string.Equals(code, unitCode, StringComparison.OrdinalIgnoreCase);

        return facts with
        {
            Units = facts.Units.Where(unit => Matches(unit.Code)).ToArray(),
            Rooms = facts.Rooms.Where(room => Matches(room.HotelUnitCode)).ToArray(),
            RoomOutages = facts.RoomOutages.Where(outage => Matches(outage.HotelUnitCode)).ToArray(),
            Stays = facts.Stays.Where(stay => Matches(stay.HotelUnitCode)).ToArray(),
            Revenues = facts.Revenues.Where(revenue => Matches(revenue.HotelUnitCode)).ToArray(),
            Invoices = facts.Invoices.Where(invoice => Matches(invoice.HotelUnitCode)).ToArray(),
            Receipts = facts.Receipts.Where(receipt => Matches(receipt.HotelUnitCode)).ToArray(),
            PaymentOrders = [],
            BudgetTargets = facts.BudgetTargets.Where(target => Matches(target.HotelUnitCode)).ToArray(),
            LedgerLines = [],
            StockMovements = facts.StockMovements.Where(movement => Matches(movement.HotelUnitCode)).ToArray(),
            OpeningStockMovements = facts.OpeningStockMovements
                .Where(movement => Matches(movement.HotelUnitCode)).ToArray(),
            Payslips = facts.Payslips.Where(payslip => Matches(payslip.HotelUnitCode)).ToArray(),
            Employees = facts.Employees.Where(employee => Matches(employee.HotelUnitCode)).ToArray(),
            Absences = facts.Absences.Where(absence => Matches(absence.HotelUnitCode)).ToArray(),
            TimeEntries = facts.TimeEntries.Where(entry => Matches(entry.HotelUnitCode)).ToArray(),
            HousekeepingTasks = facts.HousekeepingTasks.Where(task => Matches(task.HotelUnitCode)).ToArray(),
            Satisfaction = facts.Satisfaction.Where(entry => Matches(entry.HotelUnitCode)).ToArray()
        };
    }
}
