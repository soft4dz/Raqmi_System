using RaqmiSystem.Domain.Housekeeping;
using RaqmiSystem.Domain.HumanResources;
using RaqmiSystem.Domain.Kpi;
using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Les indicateurs de ressources humaines : cout du personnel, ratios sur chiffre d'affaires et
/// sur capacite, absenteisme, rotation et productivite.
///
/// QUATRE REGLES DE COMPTAGE, toutes reprises du module Paie plutot que redecidees :
/// - seuls les bulletins VALIDES comptent (une pre-paie en brouillon est recalculee de fond en
///   comble a chaque generation) ;
/// - seuls les pointages VALIDES comptent (des heures brutes non controlees ne doivent pas plus
///   alimenter un indicateur qu'un bulletin) ;
/// - seules les absences APPROUVEES comptent (une demande en attente n'est pas une absence) ;
/// - le cout employeur est celui que le bulletin imprime, jamais recompose ici : il serait
///   absurde qu'un tableau de bord et un bulletin ne disent pas le meme chiffre.
///
/// LA PERIODE DE PAIE EST MENSUELLE. Un mois de paie que la periode d'analyse touche compte EN
/// ENTIER, exactement comme un objectif budgetaire : la paie n'existe pas au grain du jour, et
/// la decouper en inventerait un.
/// </summary>
public sealed class WorkforceKpiCalculator
{
    private const string NoPayslip = "Aucun bulletin de paie valide sur la periode.";

    private const string NoRevenue = "Aucun chiffre d'affaires valide sur la periode.";

    private const string NoHeadcount = "Aucun collaborateur present sur la periode.";

    private const string NoContractualDay =
        "Aucun jour de presence contractuelle sur la periode : le taux d'absenteisme n'a pas d'objet.";

    private const string NoWorkedHour = "Aucune heure de pointage validee sur la periode.";

    private const string NoCapacity = "Aucune nuitee disponible sur la periode.";

    private const string NoOccupancy = "Aucune nuitee occupee sur la periode.";

    private const string NoAttendantDay =
        "Aucune journee de travail d'agent d'etage identifiee sur la periode.";

    public IEnumerable<KpiMeasure> Compute(
        KpiPeriod period,
        string? unitCode,
        KpiFactSet facts,
        KpiCapacity capacity)
    {
        ArgumentNullException.ThrowIfNull(period);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(capacity);

        var payslips = facts.Payslips
            .Where(payslip => payslip.Status == PayslipStatus.Validated && CoversPeriod(payslip, period))
            .ToArray();

        var payrollCost = payslips.Sum(payslip => payslip.EmployerCost);

        var revenue = facts.Revenues
            .Where(revenue => revenue.Status == DailyRevenueStatus.Validated)
            .Sum(revenue => revenue.Total);

        yield return KpiMeasure.Amount(KpiCodes.PayrollCost, unitCode, payrollCost);

        yield return KpiMeasure.Ratio(
            KpiCodes.PayrollToRevenueRate, unitCode, payrollCost, revenue, KpiMath.Percent, NoRevenue);

        yield return KpiMeasure.Ratio(
            KpiCodes.PayrollCostPerEmployee, unitCode, payrollCost, payslips.Length, KpiMath.Divide, NoPayslip);

        yield return KpiMeasure.Ratio(
            KpiCodes.PayrollCostPerAvailableRoom, unitCode, payrollCost, capacity.AvailableNights,
            KpiMath.Divide, NoCapacity);

        yield return KpiMeasure.Ratio(
            KpiCodes.PayrollCostPerOccupiedRoom, unitCode, payrollCost, capacity.OccupiedNights,
            KpiMath.Divide, NoOccupancy);

        yield return KpiMeasure.Ratio(
            KpiCodes.OvertimeRate,
            unitCode,
            payslips.Sum(payslip => payslip.OvertimeHours),
            payslips.Sum(payslip => payslip.HoursWorked),
            KpiMath.Percent,
            NoPayslip);

        foreach (var measure in ComputeHeadcountMeasures(period, unitCode, facts, revenue))
        {
            yield return measure;
        }

        var workedHours = facts.TimeEntries
            .Where(entry => entry.Status == TimeEntryStatus.Validated
                && entry.WorkDate >= period.From
                && entry.WorkDate <= period.To)
            .Sum(entry => entry.HoursWorked);

        yield return KpiMeasure.Ratio(
            KpiCodes.RevenuePerWorkedHour, unitCode, revenue, workedHours, KpiMath.Divide, NoWorkedHour);

        yield return ComputeHousekeepingProductivity(period, unitCode, facts);
    }

    private static IEnumerable<KpiMeasure> ComputeHeadcountMeasures(
        KpiPeriod period,
        string? unitCode,
        KpiFactSet facts,
        decimal revenue)
    {
        // Effectif moyen : moyenne des effectifs presents aux deux bornes. C'est la convention
        // la plus repandue et la seule verifiable a la main a partir des dossiers ; une moyenne
        // jour par jour serait plus fine mais donnerait un nombre que personne ne pourrait
        // refaire depuis l'ecran des collaborateurs.
        var openingHeadcount = facts.Employees.Count(employee => employee.IsPresentOn(period.From));
        var closingHeadcount = facts.Employees.Count(employee => employee.IsPresentOn(period.To));
        var averageHeadcount = (openingHeadcount + closingHeadcount) / 2m;

        yield return KpiMeasure.Amount(KpiCodes.HeadcountAverage, unitCode, averageHeadcount);

        var departures = facts.Employees.Count(employee =>
            employee.TerminationDate is not null
            && employee.TerminationDate.Value >= period.From
            && employee.TerminationDate.Value <= period.To);

        yield return KpiMeasure.Ratio(
            KpiCodes.TurnoverRate, unitCode, departures, averageHeadcount, KpiMath.Percent, NoHeadcount)
            .WithWarning(
                "La ventilation par motif de depart n'est pas produite : le motif de rupture est "
                + "un texte libre porte par le contrat, non un motif code.");

        yield return KpiMeasure.Ratio(
            KpiCodes.RevenuePerEmployee, unitCode, revenue, averageHeadcount, KpiMath.Divide, NoHeadcount);

        // Absenteisme : jours d'absence approuves rapportes aux jours de presence contractuelle.
        // Le calcul est en JOURS CALENDAIRES, faute de calendrier de travail dans le produit -
        // convertir en heures supposerait un rythme que personne n'a declare. La regle est dite
        // dans le catalogue et l'indicateur porte l'avertissement.
        var contractualDays = 0;

        foreach (var employee in facts.Employees)
        {
            for (var day = period.From; day <= period.To; day = day.AddDays(1))
            {
                if (employee.IsPresentOn(day))
                {
                    contractualDays++;
                }
            }
        }

        var absenceDays = facts.Absences
            .Where(absence => absence.Status == AbsenceStatus.Approved)
            .Sum(absence => absence.DaysWithin(period.From, period.To));

        yield return KpiMeasure.Ratio(
            KpiCodes.AbsenteeismRate, unitCode, absenceDays, contractualDays, KpiMath.Percent,
            NoContractualDay)
            .WithWarning(
                "Taux calcule en jours calendaires de presence contractuelle : Raqmi System ne "
                + "porte ni calendrier de travail ni planning d'equipes.");
    }

    /// <summary>
    /// Productivite d'etage : chambres traitees par JOURNEE D'AGENT, et non par agent. Un
    /// denominateur qui compterait les agents sans compter les jours ferait passer une equipe de
    /// trois personnes sur trente jours pour trois personnes tout court, et donnerait une
    /// productivite trente fois trop elevee.
    /// </summary>
    private static KpiMeasure ComputeHousekeepingProductivity(
        KpiPeriod period,
        string? unitCode,
        KpiFactSet facts)
    {
        var tasks = facts.HousekeepingTasks
            .Where(task => task.ServiceDate >= period.From && task.ServiceDate <= period.To)
            .ToArray();

        var completed = tasks
            .Where(task => task.Status is HousekeepingTaskStatus.Cleaned or HousekeepingTaskStatus.Inspected)
            .ToArray();

        var attendantDays = completed
            .Where(task => !string.IsNullOrWhiteSpace(task.AssignedTo))
            .Select(task => (task.AssignedTo!, task.ServiceDate))
            .Distinct()
            .Count();

        var measure = KpiMeasure.Ratio(
            KpiCodes.RoomsCleanedPerAttendant, unitCode, completed.Length, attendantDays,
            KpiMath.Divide, NoAttendantDay);

        var unassigned = completed.Count(task => string.IsNullOrWhiteSpace(task.AssignedTo));

        return unassigned == 0
            ? measure
            : measure.WithWarning(
                $"{unassigned} tache(s) nettoyee(s) sans agent affecte : elles comptent au "
                + "numerateur mais pas au denominateur, ce qui surestime la productivite.");
    }

    /// <summary>
    /// Le mois de paie touche-t-il la periode d'analyse ? Bornes inclusives des deux cotes : un
    /// mois partiellement couvert compte en entier, meme regle que les objectifs budgetaires.
    /// </summary>
    private static bool CoversPeriod(KpiPayslipFact payslip, KpiPeriod period)
    {
        var firstOfMonth = new DateOnly(payslip.Year, payslip.Month, 1);
        var lastOfMonth = firstOfMonth.AddMonths(1).AddDays(-1);

        return firstOfMonth <= period.To && lastOfMonth >= period.From;
    }
}
