using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Kpi;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Budgeting;
using RaqmiSystem.Domain.Accounting;
using RaqmiSystem.Domain.Crm;
using RaqmiSystem.Domain.Housekeeping;
using RaqmiSystem.Domain.HumanResources;
using RaqmiSystem.Domain.Inventory;
using RaqmiSystem.Domain.Kpi;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Revenue;
using RaqmiSystem.Domain.Treasury;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Kpi;

/// <summary>
/// Rapatrie les faits d'une periode depuis les modules proprietaires et les traduit dans le
/// vocabulaire neutre du moteur KPI (<see cref="KpiFactSet"/>).
///
/// C'est LE SEUL endroit du module qui connaisse les entites des autres modules : le jour ou le
/// PMS change encore de vocabulaire, c'est ce fichier qui bouge et rien d'autre - les
/// calculateurs lisent des faits, jamais des statuts.
///
/// Les filtres SQL poses ici sont des OPTIMISATIONS qui reproduisent les regles des
/// calculateurs (rapatrier dix ans de brouillons serait du gachis), jamais leur definition :
/// chaque calculateur reapplique sa regle sur ce qu'il recoit, et ses tests la prouvent sur des
/// donnees non filtrees - meme discipline que <c>GroupDashboardService</c>.
/// </summary>
public sealed class KpiFactLoader(RaqmiDbContext dbContext)
{
    public async Task<KpiFactSet> LoadAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var units = await LoadUnitsAsync(from, to, cancellationToken);
        var stays = await LoadStaysAsync(from, to, cancellationToken);

        return KpiFactSet.Empty with
        {
            Units = units,
            Rooms = await LoadRoomsAsync(cancellationToken),
            RoomOutages = await LoadRoomOutagesAsync(from, to, cancellationToken),
            Stays = stays,
            Revenues = await LoadRevenuesAsync(from, to, cancellationToken),
            Invoices = await LoadInvoicesAsync(to, cancellationToken),
            Receipts = await LoadReceiptsAsync(from, to, cancellationToken),
            PaymentOrders = await LoadPaymentOrdersAsync(cancellationToken),
            BudgetTargets = await LoadBudgetTargetsAsync(from, to, cancellationToken),
            LedgerLines = await LoadLedgerLinesAsync(from, to, cancellationToken),
            AccountRules = await LoadAccountRulesAsync(cancellationToken),
            StockItems = await LoadStockItemsAsync(cancellationToken),
            StockMovements = await LoadStockMovementsAsync(from, to, cancellationToken),
            OpeningStockMovements = await LoadOpeningStockAsync(from, cancellationToken),
            Payslips = await LoadPayslipsAsync(from, to, cancellationToken),
            Employees = await LoadEmployeesAsync(cancellationToken),
            Absences = await LoadAbsencesAsync(from, to, cancellationToken),
            TimeEntries = await LoadTimeEntriesAsync(from, to, cancellationToken),
            HousekeepingTasks = await LoadHousekeepingAsync(from, to, cancellationToken),
            Satisfaction = await LoadSatisfactionAsync(from, to, cancellationToken),
            ReturningCustomerCodes = await LoadReturningCustomersAsync(from, stays, cancellationToken)
        };
    }

    /// <summary>
    /// Unites actives, elargies aux unites desactivees qui portent des recettes validees dans la
    /// periode : un chiffre d'affaires realise avant la fermeture d'une unite ne doit jamais
    /// disparaitre silencieusement d'un total groupe - meme elargissement, meme raison que le
    /// tableau de bord PDG.
    /// </summary>
    private async Task<IReadOnlyCollection<KpiUnitFact>> LoadUnitsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var activeUnits = await dbContext.Set<HotelUnit>()
            .AsNoTracking()
            .Where(unit => unit.IsActive)
            .ToArrayAsync(cancellationToken);

        var activeCodes = activeUnits.Select(unit => unit.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var revenueUnitCodes = await dbContext.Set<DailyRevenue>()
            .AsNoTracking()
            .Where(revenue => revenue.BusinessDate >= from && revenue.BusinessDate <= to)
            .Select(revenue => revenue.HotelUnitCode)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        var missingCodes = revenueUnitCodes.Where(code => !activeCodes.Contains(code)).ToArray();

        var allUnits = activeUnits.AsEnumerable();

        if (missingCodes.Length > 0)
        {
            var inactiveWithFacts = await dbContext.Set<HotelUnit>()
                .AsNoTracking()
                .Where(unit => missingCodes.Contains(unit.Code))
                .ToArrayAsync(cancellationToken);

            allUnits = allUnits.Concat(inactiveWithFacts);
        }

        return allUnits
            .OrderBy(unit => unit.DisplayOrder)
            .ThenBy(unit => unit.Name)
            .Select(unit => new KpiUnitFact(unit.Code, unit.Name, unit.IsActive))
            .ToArray();
    }

    private async Task<IReadOnlyCollection<KpiRoomFact>> LoadRoomsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Set<Room>()
            .AsNoTracking()
            .Select(room => new KpiRoomFact(room.HotelUnitCode, room.Id, room.IsActive))
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// Les indisponibilites datees viennent des blocages de chambre du PMS. Deux traductions a
    /// connaitre :
    /// - la borne haute d'un blocage TERMINE est sa date de retour REELLE quand elle existe (le
    ///   blocage est fini, on sait jusqu'ou il a couru), sinon la fin prevue - la meme lecture
    ///   que <c>RoomBlock.CoversNight</c> ;
    /// - hors service technique COMME hors service d'exploitation sortent de la capacite des
    ///   INDICATEURS : la politique commerciale par unite (vendre ou non une chambre en usage
    ///   interne) regarde le moteur de disponibilite, jamais le taux d'occupation, qui doit
    ///   constater la capacite reellement exploitable - c'est la regle posee par RoomBlockKind.
    /// </summary>
    private async Task<IReadOnlyCollection<KpiRoomOutageFact>> LoadRoomOutagesAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var blocks = await dbContext.Set<RoomBlock>()
            .AsNoTracking()
            .Where(block => block.Status != RoomBlockStatus.Cancelled
                && block.StartDate <= to
                && block.EndDate > from)
            .Select(block => new
            {
                block.HotelUnitCode,
                block.RoomId,
                block.StartDate,
                block.EndDate,
                block.ActualReturnDate,
                block.Kind
            })
            .ToArrayAsync(cancellationToken);

        // Un blocage rendu AVANT d'avoir commence (retour reel anterieur au debut) n'a jamais
        // immobilise une nuit : il est ecarte plutot que de produire un intervalle inverse.
        return blocks
            .Select(block => new KpiRoomOutageFact(
                block.HotelUnitCode,
                block.RoomId,
                block.StartDate,
                block.ActualReturnDate ?? block.EndDate,
                block.Kind == RoomBlockKind.OutOfOrder))
            .Where(outage => outage.ToExclusive > outage.From)
            .ToArray();
    }

    /// <summary>
    /// Les sejours, traduits en faits neutres. Trois decisions de traduction :
    ///
    /// - PERIMETRE : les sejours qui chevauchent la fenetre (pour l'occupation) ET ceux dont
    ///   l'arrivee tombe dedans (pour ALOS, annulations, no-show), en une seule requete dont le
    ///   predicat couvre les deux cas.
    ///
    /// - CHAMBRE NON AFFECTEE : le PMS autorise desormais une reservation sans chambre precise
    ///   (vente au type). Un tel sejour bloquant tient bien UNE chambre de l'inventaire ; pour
    ///   que le comptage "chambres distinctes par nuit" le compte pour un sans le confondre avec
    ///   une chambre physique ni avec un autre sejour non affecte, il recoit l'identifiant de la
    ///   RESERVATION comme pseudo-chambre. Deux reservations non affectees la meme nuit comptent
    ///   ainsi deux chambres tenues - ce qui est exactement l'etat de l'inventaire.
    ///
    /// - STATUTS : la famille "tient l'inventaire" est lue par Status.Blocks(), la definition
    ///   unique du PMS, appliquee EN MEMOIRE - jamais recopiee dans le SQL sous forme de liste
    ///   de statuts qui pourrait deriver. Une simple demande (Inquiry) arrive donc ici avec ses
    ///   trois booleens a faux, et le calculateur l'ecarte du denominateur des annulations.
    /// </summary>
    private async Task<IReadOnlyCollection<KpiStayFact>> LoadStaysAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var reservations = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => reservation.ArrivalDate <= to
                && (reservation.DepartureDate > from || reservation.ArrivalDate >= from))
            .Select(reservation => new
            {
                reservation.Id,
                reservation.HotelUnitCode,
                reservation.RoomId,
                reservation.CustomerCode,
                reservation.ArrivalDate,
                reservation.DepartureDate,
                reservation.GuestCount,
                reservation.NightlyRateSnapshot,
                reservation.Status,
                reservation.CreatedAt
            })
            .ToArrayAsync(cancellationToken);

        return reservations
            .Select(reservation => new KpiStayFact(
                reservation.HotelUnitCode,
                reservation.RoomId ?? reservation.Id,
                reservation.CustomerCode,
                reservation.ArrivalDate,
                reservation.DepartureDate,
                reservation.GuestCount,
                reservation.NightlyRateSnapshot,
                reservation.Status.Blocks(),
                reservation.Status == ReservationStatus.Cancelled,
                reservation.Status == ReservationStatus.NoShow,
                reservation.CreatedAt))
            .ToArray();
    }

    private async Task<IReadOnlyCollection<KpiRevenueFact>> LoadRevenuesAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<DailyRevenue>()
            .AsNoTracking()
            .Where(revenue => revenue.BusinessDate >= from
                && revenue.BusinessDate <= to
                && revenue.Status == DailyRevenueStatus.Validated)
            .Select(revenue => new KpiRevenueFact(
                revenue.HotelUnitCode,
                revenue.BusinessDate,
                revenue.Accommodation,
                revenue.Food,
                revenue.Beverage,
                revenue.Other,
                revenue.Status))
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// Tout l'encours jusqu'a la fin de periode, pas seulement les factures de la periode : une
    /// vieille facture impayee precede la periode et reste pourtant due a sa fin.
    /// </summary>
    private async Task<IReadOnlyCollection<KpiInvoiceFact>> LoadInvoicesAsync(
        DateOnly to,
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<Invoice>()
            .AsNoTracking()
            .Where(invoice => invoice.InvoiceDate <= to && invoice.Status == InvoiceStatus.Issued)
            .Select(invoice => new KpiInvoiceFact(
                invoice.HotelUnitCode,
                invoice.CustomerCode,
                invoice.InvoiceDate,
                invoice.TotalInclVat,
                invoice.Status))
            .ToArrayAsync(cancellationToken);
    }

    private async Task<IReadOnlyCollection<KpiReceiptFact>> LoadReceiptsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<CashReceipt>()
            .AsNoTracking()
            .Where(receipt => receipt.ReceiptDate >= from
                && receipt.ReceiptDate <= to
                && receipt.Status == ReceiptStatus.Confirmed)
            .Select(receipt => new KpiReceiptFact(
                receipt.HotelUnitCode,
                receipt.ReceiptDate,
                receipt.Amount,
                receipt.Status))
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// Approuves ET regles, sans borne de date SQL : les decaissements de la periode se datent
    /// par le REGLEMENT (que la requete ne peut pas encadrer simplement, PaidAt etant un
    /// horodatage), et les engagements a echeance regardent l'avenir depuis aujourd'hui. Le
    /// volume reste borne : un ordre finit regle ou annule, le stock d'approuves-en-attente est
    /// petit par nature, et le calculateur filtre par dates.
    /// </summary>
    private async Task<IReadOnlyCollection<KpiPaymentOrderFact>> LoadPaymentOrdersAsync(
        CancellationToken cancellationToken)
    {
        var orders = await dbContext.Set<PaymentOrder>()
            .AsNoTracking()
            .Where(order => order.Status == PaymentOrderStatus.Approved
                || order.Status == PaymentOrderStatus.Paid)
            .Select(order => new
            {
                order.OrderDate,
                order.DueDate,
                order.PaidAt,
                order.Amount,
                order.Status
            })
            .ToArrayAsync(cancellationToken);

        return orders
            .Select(order => new KpiPaymentOrderFact(
                order.OrderDate,
                order.DueDate,
                order.PaidAt is null ? null : DateOnly.FromDateTime(order.PaidAt.Value.UtcDateTime),
                order.Amount,
                order.Status))
            .ToArray();
    }

    /// <summary>
    /// Seuls les plans FIGES (approuves ou clotures) participent : un brouillon de budget n'est
    /// une reference pour personne. Jointure plate plutot que navigation projetee, pour la meme
    /// raison de compatibilite SQLite que le tableau de bord PDG.
    /// </summary>
    private async Task<IReadOnlyCollection<KpiBudgetTargetFact>> LoadBudgetTargetsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var scopedYears = Enumerable.Range(from.Year, to.Year - from.Year + 1).ToArray();

        return await (
                from plan in dbContext.Set<BudgetPlan>().AsNoTracking()
                where scopedYears.Contains(plan.Year)
                    && (plan.Status == BudgetStatus.Approved || plan.Status == BudgetStatus.Closed)
                join line in dbContext.Set<BudgetLine>().AsNoTracking()
                    on plan.Id equals line.BudgetPlanId
                select new KpiBudgetTargetFact(
                    plan.HotelUnitCode,
                    plan.Year,
                    line.Month,
                    line.AmountTarget))
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// Seules les ecritures COMPTABILISEES entrent dans le resultat : un brouillon peut encore
    /// etre desequilibre, une ecriture abandonnee n'est jamais entree dans les livres.
    /// </summary>
    private async Task<IReadOnlyCollection<KpiLedgerFact>> LoadLedgerLinesAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        return await (
                from entry in dbContext.Set<JournalEntry>().AsNoTracking()
                where entry.Status == EntryStatus.Posted
                    && entry.EntryDate >= @from
                    && entry.EntryDate <= to
                join line in dbContext.Set<JournalEntryLine>().AsNoTracking()
                    on entry.Id equals line.JournalEntryId
                select new KpiLedgerFact(line.AccountCode, entry.EntryDate, line.Debit, line.Credit))
            .ToArrayAsync(cancellationToken);
    }

    private async Task<IReadOnlyCollection<KpiAccountRuleFact>> LoadAccountRulesAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<KpiAccountMapping>()
            .AsNoTracking()
            .Where(mapping => mapping.IsActive)
            .Select(mapping => new KpiAccountRuleFact(mapping.AccountPrefix, mapping.Group))
            .ToArrayAsync(cancellationToken);
    }

    private async Task<IReadOnlyCollection<KpiStockItemFact>> LoadStockItemsAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<StockItem>()
            .AsNoTracking()
            .Select(item => new KpiStockItemFact(item.Code, item.Category, item.IsActive))
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// Le rattachement d'un mouvement a une unite passe par son MAGASIN : le mouvement ne porte
    /// pas d'unite, le magasin si. SignedQuantity est une propriete calculee, non traduisible en
    /// SQL : le signe est recompose en memoire par la meme regle du domaine
    /// (<see cref="StockMovement.IsInbound"/>), jamais recopiee.
    /// </summary>
    private async Task<IReadOnlyCollection<KpiStockMovementFact>> LoadStockMovementsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var movements = await (
                from movement in dbContext.Set<StockMovement>().AsNoTracking()
                where movement.MovementDate >= @from && movement.MovementDate <= to
                join warehouse in dbContext.Set<Warehouse>().AsNoTracking()
                    on movement.WarehouseCode equals warehouse.Code
                select new
                {
                    warehouse.HotelUnitCode,
                    movement.WarehouseCode,
                    movement.ItemCode,
                    movement.MovementDate,
                    movement.Kind,
                    movement.Quantity,
                    movement.UnitCost,
                    movement.AdjustmentIsIncrease
                })
            .ToArrayAsync(cancellationToken);

        return movements
            .Select(movement => new KpiStockMovementFact(
                movement.HotelUnitCode,
                movement.WarehouseCode,
                movement.ItemCode,
                movement.MovementDate,
                movement.Kind,
                movement.Quantity,
                StockMovement.IsInbound(movement.Kind, movement.AdjustmentIsIncrease)
                    ? movement.Quantity
                    : -movement.Quantity,
                movement.UnitCost))
            .ToArray();
    }

    /// <summary>
    /// Le stock d'ouverture : tous les mouvements ANTERIEURS a la periode, agreges par (magasin,
    /// article, nature) pour ne pas rapatrier l'historique du registre ligne a ligne.
    ///
    /// L'agregat conserve exactement les deux grandeurs dont les calculateurs ont besoin - la
    /// QUANTITE totale et la VALEUR totale (mouvements sans cout exclus de la valeur, jamais
    /// comptes pour zero en silence) - en les repliant dans un fait synthetique dont le cout
    /// unitaire est valeur/quantite. Quantite et valeur ressortent toutes deux exactes ; seul le
    /// detail mouvement par mouvement, dont aucun indicateur n'a besoin, est perdu.
    /// </summary>
    private async Task<IReadOnlyCollection<KpiStockMovementFact>> LoadOpeningStockAsync(
        DateOnly from,
        CancellationToken cancellationToken)
    {
        var aggregates = await (
                from movement in dbContext.Set<StockMovement>().AsNoTracking()
                where movement.MovementDate < @from
                join warehouse in dbContext.Set<Warehouse>().AsNoTracking()
                    on movement.WarehouseCode equals warehouse.Code
                group movement by new
                {
                    warehouse.HotelUnitCode,
                    movement.WarehouseCode,
                    movement.ItemCode,
                    movement.Kind,
                    movement.AdjustmentIsIncrease
                }
                into grouping
                select new
                {
                    grouping.Key.HotelUnitCode,
                    grouping.Key.WarehouseCode,
                    grouping.Key.ItemCode,
                    grouping.Key.Kind,
                    grouping.Key.AdjustmentIsIncrease,
                    TotalQuantity = grouping.Sum(movement => movement.Quantity),
                    TotalValue = grouping.Sum(movement => movement.Quantity * (movement.UnitCost ?? 0m))
                })
            .ToArrayAsync(cancellationToken);

        return aggregates
            .Where(aggregate => aggregate.TotalQuantity != 0m)
            .Select(aggregate =>
            {
                var inbound = StockMovement.IsInbound(aggregate.Kind, aggregate.AdjustmentIsIncrease);

                return new KpiStockMovementFact(
                    aggregate.HotelUnitCode,
                    aggregate.WarehouseCode,
                    aggregate.ItemCode,
                    from.AddDays(-1),
                    aggregate.Kind,
                    aggregate.TotalQuantity,
                    inbound ? aggregate.TotalQuantity : -aggregate.TotalQuantity,
                    aggregate.TotalValue / aggregate.TotalQuantity);
            })
            .ToArray();
    }

    /// <summary>
    /// Les bulletins des mois de paie que la periode touche - un mois partiellement couvert
    /// compte en entier, la paie n'existant pas au grain du jour. L'unite et le departement
    /// viennent du dossier du collaborateur (affectation, puis poste), le bulletin n'en portant
    /// pas.
    /// </summary>
    private async Task<IReadOnlyCollection<KpiPayslipFact>> LoadPayslipsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var months = TouchedMonths(from, to);

        var rows = await (
                from payslip in dbContext.Set<Payslip>().AsNoTracking()
                where months.Contains(payslip.Period)
                join employee in dbContext.Set<Employee>().AsNoTracking()
                    on payslip.EmployeeId equals employee.Id
                join position in dbContext.Set<Position>().AsNoTracking()
                    on employee.PositionCode equals position.Code
                select new
                {
                    employee.HotelUnitCode,
                    position.DepartmentCode,
                    payslip.Period,
                    payslip.EmployerCost,
                    payslip.HoursWorked,
                    payslip.OvertimeHours,
                    payslip.Status
                })
            .ToArrayAsync(cancellationToken);

        return rows
            .Select(row => new KpiPayslipFact(
                row.HotelUnitCode,
                row.DepartmentCode,
                row.Period.Year,
                row.Period.Month,
                row.EmployerCost,
                row.HoursWorked,
                row.OvertimeHours,
                row.Status))
            .ToArray();
    }

    private async Task<IReadOnlyCollection<KpiEmployeeFact>> LoadEmployeesAsync(
        CancellationToken cancellationToken)
    {
        return await (
                from employee in dbContext.Set<Employee>().AsNoTracking()
                join position in dbContext.Set<Position>().AsNoTracking()
                    on employee.PositionCode equals position.Code
                select new KpiEmployeeFact(
                    employee.Id,
                    employee.HotelUnitCode,
                    position.DepartmentCode,
                    employee.HireDate,
                    employee.TerminationDate))
            .ToArrayAsync(cancellationToken);
    }

    private async Task<IReadOnlyCollection<KpiAbsenceFact>> LoadAbsencesAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        return await (
                from absence in dbContext.Set<AbsenceRequest>().AsNoTracking()
                where absence.StartDate <= to
                    && absence.EndDate >= @from
                    && absence.Status == AbsenceStatus.Approved
                join employee in dbContext.Set<Employee>().AsNoTracking()
                    on absence.EmployeeId equals employee.Id
                join position in dbContext.Set<Position>().AsNoTracking()
                    on employee.PositionCode equals position.Code
                select new KpiAbsenceFact(
                    absence.EmployeeId,
                    employee.HotelUnitCode,
                    position.DepartmentCode,
                    absence.Type,
                    absence.StartDate,
                    absence.EndDate,
                    absence.Status))
            .ToArrayAsync(cancellationToken);
    }

    private async Task<IReadOnlyCollection<KpiTimeEntryFact>> LoadTimeEntriesAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        return await (
                from entry in dbContext.Set<TimeEntry>().AsNoTracking()
                where entry.WorkDate >= @from
                    && entry.WorkDate <= to
                    && entry.Status == TimeEntryStatus.Validated
                join employee in dbContext.Set<Employee>().AsNoTracking()
                    on entry.EmployeeId equals employee.Id
                join position in dbContext.Set<Position>().AsNoTracking()
                    on employee.PositionCode equals position.Code
                select new KpiTimeEntryFact(
                    employee.HotelUnitCode,
                    position.DepartmentCode,
                    entry.WorkDate,
                    entry.HoursWorked,
                    entry.Status))
            .ToArrayAsync(cancellationToken);
    }

    private async Task<IReadOnlyCollection<KpiHousekeepingFact>> LoadHousekeepingAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<HousekeepingTask>()
            .AsNoTracking()
            .Where(task => task.ServiceDate >= from && task.ServiceDate <= to)
            .Select(task => new KpiHousekeepingFact(
                task.HotelUnitCode,
                task.ServiceDate,
                task.AssignedTo,
                task.Status))
            .ToArrayAsync(cancellationToken);
    }

    private async Task<IReadOnlyCollection<KpiSatisfactionFact>> LoadSatisfactionAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<SatisfactionEntry>()
            .AsNoTracking()
            .Where(entry => entry.SurveyDate >= from && entry.SurveyDate <= to)
            .Select(entry => new KpiSatisfactionFact(
                entry.HotelUnitCode,
                entry.SurveyDate,
                entry.Score))
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// Parmi les clients arrivant dans la periode, ceux qui portaient deja un sejour bloquant
    /// ANTERIEUR, dans n'importe quelle unite du groupe. La question est posee en deux temps -
    /// les codes de la periode d'abord, puis leur existence anterieure - pour ne jamais
    /// rapatrier l'historique complet des sejours.
    /// </summary>
    private async Task<IReadOnlySet<string>> LoadReturningCustomersAsync(
        DateOnly from,
        IReadOnlyCollection<KpiStayFact> stays,
        CancellationToken cancellationToken)
    {
        var arrivalCustomerCodes = stays
            .Where(stay => stay.BlocksInventory && stay.ArrivalDate >= from)
            .Select(stay => stay.CustomerCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (arrivalCustomerCodes.Length == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var earlier = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => reservation.ArrivalDate < from
                && arrivalCustomerCodes.Contains(reservation.CustomerCode))
            .Select(reservation => new { reservation.CustomerCode, reservation.Status })
            .ToArrayAsync(cancellationToken);

        return earlier
            .Where(candidate => candidate.Status.Blocks())
            .Select(candidate => candidate.CustomerCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Les mois de paie que la fenetre touche, bornes incluses. La fenetre etant plafonnee a
    /// 366 jours par le service, la liste tient sur au plus quatorze valeurs.
    /// </summary>
    private static PayrollMonth[] TouchedMonths(DateOnly from, DateOnly to)
    {
        var months = new List<PayrollMonth>();
        var cursor = new DateOnly(from.Year, from.Month, 1);

        while (cursor <= to)
        {
            months.Add(PayrollMonth.Parse($"{cursor.Year:D4}-{cursor.Month:D2}"));
            cursor = cursor.AddMonths(1);
        }

        return [.. months];
    }
}
