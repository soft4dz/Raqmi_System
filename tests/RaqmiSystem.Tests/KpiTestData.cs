using RaqmiSystem.Application.Kpi;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Housekeeping;
using RaqmiSystem.Domain.HumanResources;
using RaqmiSystem.Domain.Inventory;
using RaqmiSystem.Domain.Kpi;
using RaqmiSystem.Domain.Revenue;
using RaqmiSystem.Domain.Treasury;

namespace RaqmiSystem.Tests;

/// <summary>
/// Fabriques de faits pour les tests du moteur KPI. Chaque helper porte des valeurs par defaut
/// plausibles pour que chaque test ne renseigne QUE ce qu'il met en jeu : un test d'ADR ne doit
/// pas avoir a inventer une date de creation de reservation pour etre lisible.
/// </summary>
internal static class KpiTestData
{
    public const string UnitA = "HOTEL-A";
    public const string UnitB = "HOTEL-B";

    public static readonly DateOnly Jan1 = new(2026, 1, 1);
    public static readonly DateOnly Jan31 = new(2026, 1, 31);

    public static KpiPeriod January => KpiPeriod.Create(Jan1, Jan31);

    public static KpiUnitFact Unit(string code = UnitA, string? name = null, bool isActive = true)
    {
        return new KpiUnitFact(code, name ?? code, isActive);
    }

    public static KpiRoomFact Room(Guid id, string unit = UnitA, bool isActive = true)
    {
        return new KpiRoomFact(unit, id, isActive);
    }

    public static IReadOnlyCollection<KpiRoomFact> Rooms(int count, string unit = UnitA)
    {
        return Enumerable.Range(0, count)
            .Select(index => Room(RoomId(index, unit), unit))
            .ToArray();
    }

    /// <summary>
    /// Identifiant de chambre deterministe : les tests doivent pouvoir designer "la chambre 0 de
    /// l'hotel A" sans se passer un Guid de main en main, et deux executions doivent produire les
    /// memes identifiants.
    /// </summary>
    public static Guid RoomId(int index, string unit = UnitA)
    {
        var bytes = new byte[16];
        bytes[0] = (byte)index;
        bytes[1] = (byte)unit.GetHashCode(StringComparison.Ordinal);
        return new Guid(bytes);
    }

    public static KpiRoomOutageFact Outage(
        int roomIndex,
        DateOnly from,
        DateOnly toExclusive,
        string unit = UnitA,
        bool isOutOfOrder = true)
    {
        return new KpiRoomOutageFact(unit, RoomId(roomIndex, unit), from, toExclusive, isOutOfOrder);
    }

    public static KpiStayFact Stay(
        int roomIndex,
        DateOnly arrival,
        DateOnly departure,
        decimal nightlyRate = 10_000m,
        string unit = UnitA,
        string customerCode = "CLI-1",
        int guestCount = 2,
        bool blocks = true,
        bool cancelled = false,
        bool noShow = false,
        DateOnly? createdOn = null)
    {
        var created = createdOn ?? arrival.AddDays(-10);

        return new KpiStayFact(
            unit,
            RoomId(roomIndex, unit),
            customerCode,
            arrival,
            departure,
            guestCount,
            nightlyRate,
            blocks,
            cancelled,
            noShow,
            new DateTimeOffset(created.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
    }

    public static KpiRevenueFact Revenue(
        DateOnly date,
        decimal accommodation = 0m,
        decimal food = 0m,
        decimal beverage = 0m,
        decimal other = 0m,
        string unit = UnitA,
        DailyRevenueStatus status = DailyRevenueStatus.Validated)
    {
        return new KpiRevenueFact(unit, date, accommodation, food, beverage, other, status);
    }

    public static KpiInvoiceFact Invoice(
        DateOnly date,
        decimal amount,
        string unit = UnitA,
        InvoiceStatus status = InvoiceStatus.Issued,
        string customerCode = "CLI-1")
    {
        return new KpiInvoiceFact(unit, customerCode, date, amount, status);
    }

    public static KpiReceiptFact Receipt(
        DateOnly date,
        decimal amount,
        string unit = UnitA,
        ReceiptStatus status = ReceiptStatus.Confirmed)
    {
        return new KpiReceiptFact(unit, date, amount, status);
    }

    public static KpiPaymentOrderFact PaymentOrder(
        DateOnly orderDate,
        DateOnly dueDate,
        decimal amount,
        PaymentOrderStatus status = PaymentOrderStatus.Approved,
        DateOnly? paidOn = null)
    {
        return new KpiPaymentOrderFact(orderDate, dueDate, paidOn, amount, status);
    }

    public static KpiLedgerFact Ledger(string accountCode, decimal debit, decimal credit, DateOnly? date = null)
    {
        return new KpiLedgerFact(accountCode, date ?? Jan31, debit, credit);
    }

    public static KpiStockItemFact Item(
        string code,
        StockItemCategory category = StockItemCategory.Alimentaire,
        bool isActive = true)
    {
        return new KpiStockItemFact(code, category, isActive);
    }

    public static KpiStockMovementFact Consumption(
        string itemCode,
        decimal quantity,
        decimal? unitCost,
        DateOnly? date = null,
        string unit = UnitA)
    {
        return new KpiStockMovementFact(
            unit,
            "MAG-1",
            itemCode,
            date ?? Jan31,
            StockMovementKind.Consumption,
            quantity,
            -quantity,
            unitCost);
    }

    public static KpiStockMovementFact Entry(
        string itemCode,
        decimal quantity,
        decimal? unitCost,
        DateOnly? date = null,
        string unit = UnitA)
    {
        return new KpiStockMovementFact(
            unit,
            "MAG-1",
            itemCode,
            date ?? Jan1,
            StockMovementKind.PurchaseEntry,
            quantity,
            quantity,
            unitCost);
    }

    public static KpiPayslipFact Payslip(
        decimal employerCost,
        int year = 2026,
        int month = 1,
        decimal hoursWorked = 173m,
        decimal overtimeHours = 0m,
        string unit = UnitA,
        string department = "DEP-1",
        PayslipStatus status = PayslipStatus.Validated)
    {
        return new KpiPayslipFact(unit, department, year, month, employerCost, hoursWorked, overtimeHours, status);
    }

    public static KpiEmployeeFact Employee(
        DateOnly hireDate,
        DateOnly? terminationDate = null,
        string unit = UnitA,
        string department = "DEP-1")
    {
        return new KpiEmployeeFact(Guid.NewGuid(), unit, department, hireDate, terminationDate);
    }

    public static KpiAbsenceFact Absence(
        DateOnly start,
        DateOnly end,
        AbsenceType type = AbsenceType.SickLeave,
        AbsenceStatus status = AbsenceStatus.Approved,
        string unit = UnitA,
        string department = "DEP-1")
    {
        return new KpiAbsenceFact(Guid.NewGuid(), unit, department, type, start, end, status);
    }

    public static KpiTimeEntryFact TimeEntry(
        DateOnly date,
        decimal hours,
        string unit = UnitA,
        TimeEntryStatus status = TimeEntryStatus.Validated)
    {
        return new KpiTimeEntryFact(unit, "DEP-1", date, hours, status);
    }

    public static KpiHousekeepingFact HousekeepingTask(
        DateOnly date,
        string? assignedTo,
        HousekeepingTaskStatus status = HousekeepingTaskStatus.Inspected,
        string unit = UnitA)
    {
        return new KpiHousekeepingFact(unit, date, assignedTo, status);
    }

    public static KpiSatisfactionFact Survey(DateOnly date, int score, string unit = UnitA)
    {
        return new KpiSatisfactionFact(unit, date, score);
    }

    public static KpiBudgetTargetFact BudgetTarget(
        decimal amount,
        int year = 2026,
        int month = 1,
        string unit = UnitA)
    {
        return new KpiBudgetTargetFact(unit, year, month, amount);
    }

    /// <summary>
    /// Un jeu de faits construit par nommage des seules collections qui interessent le test.
    /// Tout le reste reste vide, ce qui est exactement l'etat d'un hotel sans activite sur cet
    /// axe - le cas limite que les indicateurs doivent savoir traiter.
    /// </summary>
    public static KpiFactSet Facts(
        IReadOnlyCollection<KpiUnitFact>? units = null,
        IReadOnlyCollection<KpiRoomFact>? rooms = null,
        IReadOnlyCollection<KpiRoomOutageFact>? outages = null,
        IReadOnlyCollection<KpiStayFact>? stays = null,
        IReadOnlyCollection<KpiRevenueFact>? revenues = null,
        IReadOnlyCollection<KpiInvoiceFact>? invoices = null,
        IReadOnlyCollection<KpiReceiptFact>? receipts = null,
        IReadOnlyCollection<KpiPaymentOrderFact>? paymentOrders = null,
        IReadOnlyCollection<KpiBudgetTargetFact>? budgetTargets = null,
        IReadOnlyCollection<KpiLedgerFact>? ledgerLines = null,
        IReadOnlyCollection<KpiAccountRuleFact>? accountRules = null,
        IReadOnlyCollection<KpiStockItemFact>? stockItems = null,
        IReadOnlyCollection<KpiStockMovementFact>? stockMovements = null,
        IReadOnlyCollection<KpiStockMovementFact>? openingStockMovements = null,
        IReadOnlyCollection<KpiPayslipFact>? payslips = null,
        IReadOnlyCollection<KpiEmployeeFact>? employees = null,
        IReadOnlyCollection<KpiAbsenceFact>? absences = null,
        IReadOnlyCollection<KpiTimeEntryFact>? timeEntries = null,
        IReadOnlyCollection<KpiHousekeepingFact>? housekeepingTasks = null,
        IReadOnlyCollection<KpiSatisfactionFact>? satisfaction = null,
        IEnumerable<string>? returningCustomers = null)
    {
        return KpiFactSet.Empty with
        {
            Units = units ?? [Unit()],
            Rooms = rooms ?? [],
            RoomOutages = outages ?? [],
            Stays = stays ?? [],
            Revenues = revenues ?? [],
            Invoices = invoices ?? [],
            Receipts = receipts ?? [],
            PaymentOrders = paymentOrders ?? [],
            BudgetTargets = budgetTargets ?? [],
            LedgerLines = ledgerLines ?? [],
            AccountRules = accountRules ?? [],
            StockItems = stockItems ?? [],
            StockMovements = stockMovements ?? [],
            OpeningStockMovements = openingStockMovements ?? [],
            Payslips = payslips ?? [],
            Employees = employees ?? [],
            Absences = absences ?? [],
            TimeEntries = timeEntries ?? [],
            HousekeepingTasks = housekeepingTasks ?? [],
            Satisfaction = satisfaction ?? [],
            ReturningCustomerCodes = (returningCustomers ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase)
        };
    }

    /// <summary>Le mapping de comptes minimal permettant de calculer un GOP et un EBE.</summary>
    public static IReadOnlyCollection<KpiAccountRuleFact> StandardAccountRules =>
    [
        new("7", KpiAccountGroup.Revenue),
        new("60", KpiAccountGroup.DepartmentalExpense),
        new("61", KpiAccountGroup.UndistributedExpense),
        new("613", KpiAccountGroup.FixedCharge),
        new("68", KpiAccountGroup.DepreciationAndProvision)
    ];

    public static KpiMeasure Measure(KpiComputation computation, string code, string? unit = null)
    {
        return computation.Require(code, unit);
    }
}
