namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Tout ce que le moteur a besoin de savoir pour calculer une periode : les faits bruts des
/// modules sources, deja rapatries, jamais retravailles.
///
/// La separation est deliberee et suit celle deja etablie par le tableau de bord groupe : un
/// SERVICE va chercher les donnees (et peut donc les filtrer cote base pour ne pas rapatrier
/// dix ans de brouillons), un CALCULATEUR PUR les combine. Toutes les regles de comptage vivent
/// dans le calculateur, qui les reapplique sur ce qu'il recoit : les tests unitaires les
/// prouvent donc sur des donnees non filtrees, et une optimisation SQL ne peut pas devenir
/// silencieusement la definition d'un indicateur.
/// </summary>
public sealed record KpiFactSet
{
    public required IReadOnlyCollection<KpiUnitFact> Units { get; init; }

    public required IReadOnlyCollection<KpiRoomFact> Rooms { get; init; }

    public required IReadOnlyCollection<KpiRoomOutageFact> RoomOutages { get; init; }

    public required IReadOnlyCollection<KpiStayFact> Stays { get; init; }

    public required IReadOnlyCollection<KpiRevenueFact> Revenues { get; init; }

    public required IReadOnlyCollection<KpiInvoiceFact> Invoices { get; init; }

    public required IReadOnlyCollection<KpiReceiptFact> Receipts { get; init; }

    public required IReadOnlyCollection<KpiPaymentOrderFact> PaymentOrders { get; init; }

    public required IReadOnlyCollection<KpiBudgetTargetFact> BudgetTargets { get; init; }

    public required IReadOnlyCollection<KpiLedgerFact> LedgerLines { get; init; }

    public required IReadOnlyCollection<KpiAccountRuleFact> AccountRules { get; init; }

    public required IReadOnlyCollection<KpiStockItemFact> StockItems { get; init; }

    /// <summary>
    /// Mouvements de stock de la periode, pour les consommations valorisees.
    /// </summary>
    public required IReadOnlyCollection<KpiStockMovementFact> StockMovements { get; init; }

    /// <summary>
    /// Mouvements de stock ANTERIEURS a la periode, deja agreges par article et magasin sous
    /// forme de quantite et de valeur au dernier cout connu. Ils donnent le stock d'ouverture,
    /// indispensable a la rotation et au taux de rupture, sans obliger a rapatrier tout
    /// l'historique du registre.
    /// </summary>
    public required IReadOnlyCollection<KpiStockMovementFact> OpeningStockMovements { get; init; }

    public required IReadOnlyCollection<KpiPayslipFact> Payslips { get; init; }

    public required IReadOnlyCollection<KpiEmployeeFact> Employees { get; init; }

    public required IReadOnlyCollection<KpiAbsenceFact> Absences { get; init; }

    public required IReadOnlyCollection<KpiTimeEntryFact> TimeEntries { get; init; }

    public required IReadOnlyCollection<KpiHousekeepingFact> HousekeepingTasks { get; init; }

    public required IReadOnlyCollection<KpiSatisfactionFact> Satisfaction { get; init; }

    /// <summary>
    /// Codes clients ayant deja sejourne AVANT le debut de la periode, dans n'importe quelle
    /// unite du groupe. Un ensemble plutot que la liste des sejours anterieurs : la seule
    /// question posee est "ce client etait-il deja venu ?", et rapatrier l'historique complet
    /// des sejours pour y repondre serait disproportionne.
    /// </summary>
    public required IReadOnlySet<string> ReturningCustomerCodes { get; init; }

    /// <summary>Un jeu de faits entierement vide - utile aux tests et aux periodes sans activite.</summary>
    public static KpiFactSet Empty { get; } = new()
    {
        Units = [],
        Rooms = [],
        RoomOutages = [],
        Stays = [],
        Revenues = [],
        Invoices = [],
        Receipts = [],
        PaymentOrders = [],
        BudgetTargets = [],
        LedgerLines = [],
        AccountRules = [],
        StockItems = [],
        StockMovements = [],
        OpeningStockMovements = [],
        Payslips = [],
        Employees = [],
        Absences = [],
        TimeEntries = [],
        HousekeepingTasks = [],
        Satisfaction = [],
        ReturningCustomerCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    };
}
