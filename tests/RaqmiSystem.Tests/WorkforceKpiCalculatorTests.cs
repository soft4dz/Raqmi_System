using RaqmiSystem.Application.Kpi;
using RaqmiSystem.Domain.Housekeeping;
using RaqmiSystem.Domain.HumanResources;
using RaqmiSystem.Domain.Kpi;
using static RaqmiSystem.Tests.KpiTestData;

namespace RaqmiSystem.Tests;

/// <summary>
/// Les indicateurs de ressources humaines : cout du personnel, ratios, absenteisme, rotation et
/// productivite.
/// </summary>
public sealed class WorkforceKpiCalculatorTests
{
    private readonly WorkforceKpiCalculator calculator = new();

    private IReadOnlyDictionary<string, KpiMeasure> Compute(KpiFactSet facts, KpiCapacity? capacity = null)
    {
        return calculator.Compute(January, UnitA, facts, capacity ?? KpiCapacity.Empty)
            .ToDictionary(measure => measure.Code);
    }

    [Fact]
    public void Payroll_counts_the_employer_cost_of_validated_payslips_only()
    {
        var facts = Facts(payslips:
        [
            Payslip(200_000m),
            Payslip(500_000m, status: PayslipStatus.Draft)
        ]);

        Assert.Equal(200_000m, Compute(facts)[KpiCodes.PayrollCost].Value);
    }

    [Fact]
    public void A_payroll_month_the_period_touches_counts_in_full()
    {
        // La paie n'existe pas au grain du jour ; la decouper en inventerait un.
        var facts = Facts(payslips: [Payslip(200_000m, month: 1), Payslip(300_000m, month: 2)]);

        Assert.Equal(200_000m, Compute(facts)[KpiCodes.PayrollCost].Value);
    }

    [Fact]
    public void Payroll_to_revenue_is_the_ratio_a_hotelier_pilots_on()
    {
        var facts = Facts(
            revenues: [Revenue(Jan1, accommodation: 1_000_000m)],
            payslips: [Payslip(350_000m)]);

        Assert.Equal(35m, Compute(facts)[KpiCodes.PayrollToRevenueRate].Value);
    }

    [Fact]
    public void Payroll_per_room_uses_the_capacity_of_the_period()
    {
        var facts = Facts(payslips: [Payslip(600_000m)]);

        var measures = Compute(facts, new KpiCapacity(AvailableNights: 1_200, OccupiedNights: 600));

        Assert.Equal(500m, measures[KpiCodes.PayrollCostPerAvailableRoom].Value);
        Assert.Equal(1_000m, measures[KpiCodes.PayrollCostPerOccupiedRoom].Value);
    }

    [Fact]
    public void Payroll_per_room_without_capacity_is_a_dash()
    {
        var measure = Compute(Facts(payslips: [Payslip(600_000m)]))[KpiCodes.PayrollCostPerAvailableRoom];

        Assert.Null(measure.Value);
        Assert.Equal(KpiQuality.MissingData, measure.Quality);
    }

    [Fact]
    public void Average_headcount_is_the_mean_of_both_period_bounds()
    {
        var facts = Facts(employees:
        [
            Employee(new DateOnly(2024, 1, 1)),
            Employee(new DateOnly(2024, 1, 1), terminationDate: new DateOnly(2026, 1, 15)),
            Employee(new DateOnly(2026, 1, 20))
        ]);

        // Au 1er janvier : 2 presents. Au 31 janvier : 2 presents (un parti, un arrive).
        Assert.Equal(2m, Compute(facts)[KpiCodes.HeadcountAverage].Value);
    }

    [Fact]
    public void Turnover_is_departures_over_the_average_headcount()
    {
        var facts = Facts(employees:
        [
            Employee(new DateOnly(2024, 1, 1)),
            Employee(new DateOnly(2024, 1, 1)),
            Employee(new DateOnly(2024, 1, 1), terminationDate: new DateOnly(2026, 1, 15))
        ]);

        // Effectif moyen (3 + 2) / 2 = 2,5 ; un depart : 40 %.
        var turnover = Compute(facts)[KpiCodes.TurnoverRate];

        Assert.Equal(40m, turnover.Value);

        // La ventilation par motif est annoncee comme non produite, plutot que fabriquee.
        Assert.Equal(KpiQuality.Partial, turnover.Quality);
        Assert.Contains(turnover.MissingData, reason => reason.Contains("motif de depart", StringComparison.Ordinal));
    }

    [Fact]
    public void Absenteeism_is_absence_days_over_contractual_presence_days()
    {
        var facts = Facts(
            employees: [Employee(new DateOnly(2024, 1, 1))],
            absences: [Absence(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 7))]);

        // 3 jours d'absence sur 31 jours de presence contractuelle.
        Assert.Equal(9.68m, Compute(facts)[KpiCodes.AbsenteeismRate].Value);
    }

    [Fact]
    public void Only_approved_absences_count()
    {
        var facts = Facts(
            employees: [Employee(new DateOnly(2024, 1, 1))],
            absences:
            [
                Absence(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 7)),
                Absence(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 20), status: AbsenceStatus.Requested),
                Absence(new DateOnly(2026, 1, 21), new DateOnly(2026, 1, 25), status: AbsenceStatus.Rejected)
            ]);

        Assert.Equal(9.68m, Compute(facts)[KpiCodes.AbsenteeismRate].Value);
    }

    [Fact]
    public void An_absence_straddling_the_period_counts_only_its_days_inside()
    {
        var facts = Facts(
            employees: [Employee(new DateOnly(2024, 1, 1))],
            absences: [Absence(new DateOnly(2025, 12, 28), new DateOnly(2026, 1, 3))]);

        // Trois jours en janvier seulement.
        Assert.Equal(9.68m, Compute(facts)[KpiCodes.AbsenteeismRate].Value);
    }

    [Fact]
    public void Revenue_per_worked_hour_uses_validated_time_entries_only()
    {
        var facts = Facts(
            revenues: [Revenue(Jan1, accommodation: 10_000m)],
            timeEntries:
            [
                TimeEntry(Jan1, 100m),
                TimeEntry(Jan1, 900m, status: TimeEntryStatus.Draft)
            ]);

        Assert.Equal(100m, Compute(facts)[KpiCodes.RevenuePerWorkedHour].Value);
    }

    [Fact]
    public void Overtime_rate_is_read_on_the_payslips()
    {
        var facts = Facts(payslips: [Payslip(100_000m, hoursWorked: 160m, overtimeHours: 16m)]);

        Assert.Equal(10m, Compute(facts)[KpiCodes.OvertimeRate].Value);
    }

    [Fact]
    public void Housekeeping_productivity_counts_attendant_days_not_attendants()
    {
        // Deux agents sur deux jours = quatre journees d'agent, pas deux agents.
        var facts = Facts(housekeepingTasks:
        [
            HousekeepingTask(Jan1, "AGENT-1"),
            HousekeepingTask(Jan1, "AGENT-1"),
            HousekeepingTask(Jan1, "AGENT-2"),
            HousekeepingTask(Jan1.AddDays(1), "AGENT-1"),
            HousekeepingTask(Jan1.AddDays(1), "AGENT-2"),
            HousekeepingTask(Jan1.AddDays(1), "AGENT-2")
        ]);

        Assert.Equal(1.5m, Compute(facts)[KpiCodes.RoomsCleanedPerAttendant].Value);
    }

    [Fact]
    public void A_cancelled_cleaning_task_is_not_a_cleaned_room()
    {
        var facts = Facts(housekeepingTasks:
        [
            HousekeepingTask(Jan1, "AGENT-1"),
            HousekeepingTask(Jan1, "AGENT-1", HousekeepingTaskStatus.Cancelled),
            HousekeepingTask(Jan1, "AGENT-1", HousekeepingTaskStatus.Pending)
        ]);

        Assert.Equal(1m, Compute(facts)[KpiCodes.RoomsCleanedPerAttendant].Value);
    }

    [Fact]
    public void A_cleaned_room_without_an_attendant_is_flagged_as_overstating_productivity()
    {
        var facts = Facts(housekeepingTasks:
        [
            HousekeepingTask(Jan1, "AGENT-1"),
            HousekeepingTask(Jan1, null)
        ]);

        var measure = Compute(facts)[KpiCodes.RoomsCleanedPerAttendant];

        Assert.Equal(2m, measure.Value);
        Assert.Equal(KpiQuality.Partial, measure.Quality);
        Assert.Contains(measure.MissingData, reason => reason.Contains("sans agent affecte", StringComparison.Ordinal));
    }

    [Fact]
    public void A_period_without_any_payroll_reports_dashes_not_zeros_on_the_ratios()
    {
        var measures = Compute(Facts());

        Assert.Equal(0m, measures[KpiCodes.PayrollCost].Value);
        Assert.Null(measures[KpiCodes.PayrollCostPerEmployee].Value);
        Assert.Null(measures[KpiCodes.PayrollToRevenueRate].Value);
        Assert.Null(measures[KpiCodes.TurnoverRate].Value);
        Assert.Null(measures[KpiCodes.AbsenteeismRate].Value);
    }
}
