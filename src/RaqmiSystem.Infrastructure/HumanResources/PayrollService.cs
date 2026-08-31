using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.HumanResources;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.HumanResources;
using RaqmiSystem.Infrastructure.Persistence;
using System.Data;
using System.Text.Json;

namespace RaqmiSystem.Infrastructure.HumanResources;

/// <summary>
/// Payroll side of the HR module: statutory parameter versions, bonuses, the monthly run,
/// payslips, and the validation then closing of a period.
///
/// THE RUN IS AN UPSERT, NOT AN APPEND. Draft payslips are recomputed, validated ones are left
/// untouched and counted as skipped. That is what makes "correct a time entry and regenerate" the
/// normal way to work, without any risk of rewriting a payslip already signed off.
///
/// THE RUN IS SERIALIZABLE. It reads time entries, absences, bonuses and contracts, then writes
/// one payslip per employee. Two concurrent runs on the same period would interleave those reads
/// and writes and could produce payslips computed from half of one state and half of another, so
/// the whole run shares one Serializable transaction and a serialization abort surfaces as a
/// retryable conflict instead of a silently wrong month. The unique index
/// ux_hr_payslips_period_employee is the backstop.
/// </summary>
public sealed class PayrollService(
    RaqmiDbContext dbContext,
    IAuditLogWriter auditLogWriter) : IPayrollService
{
    private const string ParameterSetsEntity = "hr.payroll_parameter_sets";

    private const string BonusesEntity = "hr.payroll_bonuses";

    private const string PayslipsEntity = "hr.payslips";

    private const string PeriodsEntity = "hr.payroll_periods";

    private const string ConcurrentPayrollMutationRefused =
        "A concurrent operation modified the same payroll period, so this change was rolled back "
        + "and nothing was modified. Reload and try again.";

    public async Task<IReadOnlyCollection<PayrollParameterSetResponse>> ListParameterSetsAsync(
        CancellationToken cancellationToken)
    {
        var sets = await dbContext.Set<PayrollParameterSet>()
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        return sets
            .OrderByDescending(set => set.EffectiveFrom)
            .Select(Map)
            .ToArray();
    }

    public async Task<ApplicationResult<PayrollParameterSetResponse>> CreateParameterSetAsync(
        CreatePayrollParameterSetRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (!PayrollMonth.TryParse(request.EffectiveFrom, out var effectiveFrom))
        {
            return ApplicationResult<PayrollParameterSetResponse>.Validation(
                "The effective period must use the YYYY-MM format (for example 2026-01).");
        }

        PayrollParameterSet set;

        try
        {
            set = new PayrollParameterSet(
                effectiveFrom,
                request.Label,
                request.MonthlyReferenceHours,
                request.OvertimeMultiplier,
                request.ReferenceDaysPerMonth,
                request.EmployeeSocialRate,
                request.EmployerSocialRate,
                request.WorkAccidentRate,
                request.UnemploymentInsuranceRate,
                request.VocationalTrainingRate,
                request.IncomeTaxAbatement,
                request.IncomeTaxAbatementPerChild,
                request.MinimumWage);

            set.ReplaceBrackets(request.Brackets
                .Select(bracket => new IncomeTaxBracket(bracket.UpperBound, bracket.Rate))
                .ToArray());
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<PayrollParameterSetResponse>.Validation(ex.Message);
        }

        var exists = await dbContext.Set<PayrollParameterSet>()
            .AnyAsync(current => current.EffectiveFrom == effectiveFrom, cancellationToken);

        if (exists)
        {
            return ApplicationResult<PayrollParameterSetResponse>.Conflict(
                $"A payroll parameter set already takes effect from {effectiveFrom}.");
        }

        set.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<PayrollParameterSet>().Add(set);

        try
        {
            await WriteAuditAsync(
                "hr.payroll_parameters.created",
                ParameterSetsEntity,
                set.Id,
                context,
                new
                {
                    EffectiveFrom = effectiveFrom.ToString(),
                    set.Label,
                    set.EmployeeSocialRate,
                    set.EmployerSocialRate,
                    set.IncomeTaxAbatement,
                    set.MinimumWage,
                    BracketCount = set.Brackets.Count
                },
                cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<PayrollParameterSetResponse>.Conflict(
                "A concurrent operation already registered a parameter set for this effective period.");
        }

        return ApplicationResult<PayrollParameterSetResponse>.Success(Map(set));
    }

    public async Task<IReadOnlyCollection<PayrollPeriodResponse>> ListPeriodsAsync(
        CancellationToken cancellationToken)
    {
        var periods = await dbContext.Set<PayrollPeriod>()
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        var responses = new List<PayrollPeriodResponse>(periods.Length);

        foreach (var period in periods.OrderByDescending(current => current.Period))
        {
            responses.Add(await MapAsync(period, cancellationToken));
        }

        return responses;
    }

    public async Task<ApplicationResult<PayrollPeriodResponse>> GetPeriodAsync(
        string period,
        CancellationToken cancellationToken)
    {
        if (!PayrollMonth.TryParse(period, out var month))
        {
            return ApplicationResult<PayrollPeriodResponse>.Validation(InvalidPeriodMessage);
        }

        var row = await dbContext.Set<PayrollPeriod>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Period == month, cancellationToken);

        // A month nobody has touched yet is reported as an open period with no payslip rather than
        // as a 404: from the point of view of the operator it exists, it is simply empty.
        if (row is null)
        {
            return ApplicationResult<PayrollPeriodResponse>.Success(new PayrollPeriodResponse(
                month.ToString(),
                PayrollPeriodStatus.Draft,
                0,
                0,
                0m,
                0m,
                0m,
                null,
                null,
                null,
                null));
        }

        return ApplicationResult<PayrollPeriodResponse>.Success(await MapAsync(row, cancellationToken));
    }

    public async Task<ApplicationResult<IReadOnlyCollection<PayrollBonusResponse>>> ListBonusesAsync(
        string period,
        CancellationToken cancellationToken)
    {
        if (!PayrollMonth.TryParse(period, out var month))
        {
            return ApplicationResult<IReadOnlyCollection<PayrollBonusResponse>>.Validation(InvalidPeriodMessage);
        }

        var rows = await dbContext.Set<PayrollBonus>()
            .AsNoTracking()
            .Where(bonus => bonus.Period == month)
            .Join(
                dbContext.Set<Employee>().AsNoTracking(),
                bonus => bonus.EmployeeId,
                employee => employee.Id,
                (bonus, employee) => new { bonus, employee })
            .OrderBy(row => row.employee.LastName)
            .ThenBy(row => row.bonus.Code)
            .ToArrayAsync(cancellationToken);

        return ApplicationResult<IReadOnlyCollection<PayrollBonusResponse>>.Success(
            rows.Select(row => Map(row.bonus, row.employee)).ToArray());
    }

    public async Task<ApplicationResult<PayrollBonusResponse>> AddBonusAsync(
        string period,
        CreateBonusRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (!PayrollMonth.TryParse(period, out var month))
        {
            return ApplicationResult<PayrollBonusResponse>.Validation(InvalidPeriodMessage);
        }

        var lockError = await CheckPeriodOpenAsync(month, cancellationToken);

        if (lockError is not null)
        {
            return ApplicationResult<PayrollBonusResponse>.Conflict(lockError);
        }

        var employee = await dbContext.Set<Employee>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == request.EmployeeId, cancellationToken);

        if (employee is null)
        {
            return ApplicationResult<PayrollBonusResponse>.NotFound("Employee was not found.");
        }

        PayrollBonus bonus;

        try
        {
            bonus = new PayrollBonus(month, request.EmployeeId, request.Code, request.Label, request.Amount);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<PayrollBonusResponse>.Validation(ex.Message);
        }

        bonus.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<PayrollBonus>().Add(bonus);

        await WriteAuditAsync(
            "hr.payroll_bonus.created",
            BonusesEntity,
            bonus.Id,
            context,
            new { Period = month.ToString(), employee.EmployeeNumber, bonus.Code, bonus.Amount },
            cancellationToken);

        return ApplicationResult<PayrollBonusResponse>.Success(Map(bonus, employee));
    }

    public async Task<ApplicationResult<PayrollBonusResponse>> DeleteBonusAsync(
        string period,
        Guid bonusId,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (!PayrollMonth.TryParse(period, out var month))
        {
            return ApplicationResult<PayrollBonusResponse>.Validation(InvalidPeriodMessage);
        }

        var lockError = await CheckPeriodOpenAsync(month, cancellationToken);

        if (lockError is not null)
        {
            return ApplicationResult<PayrollBonusResponse>.Conflict(lockError);
        }

        var bonus = await dbContext.Set<PayrollBonus>()
            .SingleOrDefaultAsync(
                current => current.Id == bonusId && current.Period == month,
                cancellationToken);

        if (bonus is null)
        {
            return ApplicationResult<PayrollBonusResponse>.NotFound("Bonus was not found.");
        }

        var employee = await dbContext.Set<Employee>()
            .AsNoTracking()
            .SingleAsync(current => current.Id == bonus.EmployeeId, cancellationToken);

        var response = Map(bonus, employee);

        dbContext.Set<PayrollBonus>().Remove(bonus);

        await WriteAuditAsync(
            "hr.payroll_bonus.deleted",
            BonusesEntity,
            bonus.Id,
            context,
            new { Period = month.ToString(), employee.EmployeeNumber, bonus.Code, bonus.Amount },
            cancellationToken);

        return ApplicationResult<PayrollBonusResponse>.Success(response);
    }

    public async Task<ApplicationResult<PrePayrollRunResponse>> GeneratePrePayrollAsync(
        string period,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (!PayrollMonth.TryParse(period, out var month))
        {
            return ApplicationResult<PrePayrollRunResponse>.Validation(InvalidPeriodMessage);
        }

        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var payrollPeriod = await LoadOrCreatePeriodAsync(month, context, cancellationToken);

            if (payrollPeriod.IsClosed)
            {
                return ApplicationResult<PrePayrollRunResponse>.Conflict(ClosedPeriodMessage(month));
            }

            var parameterSet = await ResolveParameterSetAsync(month, cancellationToken);

            if (parameterSet is null)
            {
                return ApplicationResult<PrePayrollRunResponse>.Validation(
                    $"No payroll parameter set takes effect on or before {month}. "
                    + "Register the statutory parameters before running the payroll.");
            }

            var parameters = parameterSet.ToParameters();
            var facts = await LoadMonthFactsAsync(month, cancellationToken);

            var generated = 0;
            var updated = 0;
            var skipped = 0;
            var withoutContract = 0;
            var warnings = new List<string>();

            foreach (var employee in facts.Employees)
            {
                if (!employee.IsPayableFor(month))
                {
                    continue;
                }

                if (!facts.Contracts.TryGetValue(employee.Id, out var contract))
                {
                    withoutContract++;
                    warnings.Add(
                        $"{employee.EmployeeNumber} - {employee.FullName}: no active contract covering {month}, "
                        + "no payslip was produced.");
                    continue;
                }

                var existing = facts.Payslips.GetValueOrDefault(employee.Id);

                // The whole point of the draft/validated split: a payslip already signed off is
                // never recomputed, and the operator is told how many were left alone.
                if (existing is not null && !existing.IsDraft)
                {
                    skipped++;
                    continue;
                }

                var hours = facts.Hours.GetValueOrDefault(employee.Id);
                var unpaidDays = facts.UnpaidDays.GetValueOrDefault(employee.Id);
                var bonuses = facts.Bonuses.GetValueOrDefault(employee.Id);

                var computation = AlgerianPayrollEngine.Compute(
                    contract.GrossSalary,
                    hours,
                    unpaidDays,
                    bonuses,
                    employee.DependentChildren,
                    parameters);

                if (computation.BelowMinimumWage)
                {
                    warnings.Add(
                        $"{employee.EmployeeNumber} - {employee.FullName}: contractual gross "
                        + $"({contract.GrossSalary:0.00}) is below the minimum wage "
                        + $"({parameters.MinimumWage:0.00}).");
                }

                if (existing is null)
                {
                    var payslip = new Payslip(month, employee.Id, computation);
                    payslip.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
                    dbContext.Set<Payslip>().Add(payslip);
                    generated++;
                }
                else
                {
                    existing.Apply(computation);
                    existing.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);
                    updated++;
                }
            }

            var totals = await SaveAndSummariseAsync(month, context, generated, updated, skipped, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<PrePayrollRunResponse>.Success(new PrePayrollRunResponse(
                month.ToString(),
                generated,
                updated,
                skipped,
                withoutContract,
                totals.TaxableGross,
                totals.NetPay,
                totals.EmployerCost,
                warnings));
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<PrePayrollRunResponse>.Conflict(ConcurrentPayrollMutationRefused);
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<PrePayrollRunResponse>.Conflict(ConcurrentPayrollMutationRefused);
        }
    }

    public async Task<ApplicationResult<IReadOnlyCollection<PayslipResponse>>> ListPayslipsAsync(
        string period,
        Guid? employeeId,
        CancellationToken cancellationToken)
    {
        if (!PayrollMonth.TryParse(period, out var month))
        {
            return ApplicationResult<IReadOnlyCollection<PayslipResponse>>.Validation(InvalidPeriodMessage);
        }

        var query = dbContext.Set<Payslip>()
            .AsNoTracking()
            .Where(payslip => payslip.Period == month);

        if (employeeId is not null)
        {
            query = query.Where(payslip => payslip.EmployeeId == employeeId);
        }

        var rows = await query
            .Join(
                dbContext.Set<Employee>().AsNoTracking(),
                payslip => payslip.EmployeeId,
                employee => employee.Id,
                (payslip, employee) => new { payslip, employee })
            .OrderBy(row => row.employee.LastName)
            .ThenBy(row => row.employee.FirstName)
            .ToArrayAsync(cancellationToken);

        return ApplicationResult<IReadOnlyCollection<PayslipResponse>>.Success(
            rows.Select(row => Map(row.payslip, row.employee)).ToArray());
    }

    public async Task<ApplicationResult<PayslipResponse>> ValidatePayslipAsync(
        string period,
        Guid payslipId,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (!PayrollMonth.TryParse(period, out var month))
        {
            return ApplicationResult<PayslipResponse>.Validation(InvalidPeriodMessage);
        }

        var lockError = await CheckPeriodOpenAsync(month, cancellationToken);

        if (lockError is not null)
        {
            return ApplicationResult<PayslipResponse>.Conflict(lockError);
        }

        var payslip = await dbContext.Set<Payslip>()
            .SingleOrDefaultAsync(
                current => current.Id == payslipId && current.Period == month,
                cancellationToken);

        if (payslip is null)
        {
            return ApplicationResult<PayslipResponse>.NotFound("Payslip was not found.");
        }

        try
        {
            payslip.Validate(context.UserName, DateTimeOffset.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return ApplicationResult<PayslipResponse>.Validation(ex.Message);
        }

        payslip.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        var employee = await dbContext.Set<Employee>()
            .AsNoTracking()
            .SingleAsync(current => current.Id == payslip.EmployeeId, cancellationToken);

        await WriteAuditAsync(
            "hr.payslip.validated",
            PayslipsEntity,
            payslip.Id,
            context,
            new { Period = month.ToString(), employee.EmployeeNumber, payslip.TaxableGross, payslip.NetPay },
            cancellationToken);

        return ApplicationResult<PayslipResponse>.Success(Map(payslip, employee));
    }

    public async Task<ApplicationResult<PayrollPeriodResponse>> ValidatePeriodAsync(
        string period,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (!PayrollMonth.TryParse(period, out var month))
        {
            return ApplicationResult<PayrollPeriodResponse>.Validation(InvalidPeriodMessage);
        }

        var payrollPeriod = await dbContext.Set<PayrollPeriod>()
            .SingleOrDefaultAsync(current => current.Period == month, cancellationToken);

        if (payrollPeriod is null)
        {
            return ApplicationResult<PayrollPeriodResponse>.NotFound(
                $"Payroll period {month} has not been generated yet.");
        }

        var payslips = await dbContext.Set<Payslip>()
            .AsNoTracking()
            .Where(payslip => payslip.Period == month)
            .ToArrayAsync(cancellationToken);

        if (payslips.Length == 0)
        {
            return ApplicationResult<PayrollPeriodResponse>.Validation(
                $"Payroll period {month} holds no payslip. Run the pre-payroll first.");
        }

        var drafts = payslips.Count(payslip => payslip.IsDraft);

        // The guard that stands between a forgotten payslip and a closed month. The count is in
        // the message because "some payslips are still drafts" leaves the operator hunting.
        if (drafts > 0)
        {
            return ApplicationResult<PayrollPeriodResponse>.Validation(
                $"{drafts} payslip(s) of {month} are still drafts. Validate them before validating the period.");
        }

        try
        {
            payrollPeriod.Validate(payslips.Length, context.UserName, DateTimeOffset.UtcNow);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<PayrollPeriodResponse>.Validation(ex.Message);
        }

        payrollPeriod.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "hr.payroll_period.validated",
            PeriodsEntity,
            payrollPeriod.Id,
            context,
            new { Period = month.ToString(), PayslipCount = payslips.Length },
            cancellationToken);

        return ApplicationResult<PayrollPeriodResponse>.Success(
            await MapAsync(payrollPeriod, cancellationToken));
    }

    public async Task<ApplicationResult<PayrollPeriodResponse>> ClosePeriodAsync(
        string period,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (!PayrollMonth.TryParse(period, out var month))
        {
            return ApplicationResult<PayrollPeriodResponse>.Validation(InvalidPeriodMessage);
        }

        var payrollPeriod = await dbContext.Set<PayrollPeriod>()
            .SingleOrDefaultAsync(current => current.Period == month, cancellationToken);

        if (payrollPeriod is null)
        {
            return ApplicationResult<PayrollPeriodResponse>.NotFound(
                $"Payroll period {month} has not been generated yet.");
        }

        try
        {
            payrollPeriod.Close(context.UserName, DateTimeOffset.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return ApplicationResult<PayrollPeriodResponse>.Validation(ex.Message);
        }

        payrollPeriod.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "hr.payroll_period.closed",
            PeriodsEntity,
            payrollPeriod.Id,
            context,
            new { Period = month.ToString(), payrollPeriod.PayslipCount },
            cancellationToken);

        return ApplicationResult<PayrollPeriodResponse>.Success(
            await MapAsync(payrollPeriod, cancellationToken));
    }

    /// <summary>
    /// Resolves the parameter version governing a period: the most recent set effective at or
    /// before it. The sets are loaded and compared in memory on purpose - there is one row per
    /// finance act, a handful in the life of an installation, and the period is stored as text,
    /// so an in-database range comparison would buy nothing and read worse.
    /// </summary>
    private async Task<PayrollParameterSet?> ResolveParameterSetAsync(
        PayrollMonth month,
        CancellationToken cancellationToken)
    {
        var sets = await dbContext.Set<PayrollParameterSet>()
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        return sets
            .Where(set => set.EffectiveFrom <= month)
            .OrderByDescending(set => set.EffectiveFrom)
            .FirstOrDefault();
    }

    private async Task<PayrollPeriod> LoadOrCreatePeriodAsync(
        PayrollMonth month,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var period = await dbContext.Set<PayrollPeriod>()
            .SingleOrDefaultAsync(current => current.Period == month, cancellationToken);

        if (period is not null)
        {
            return period;
        }

        period = new PayrollPeriod(month);
        period.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<PayrollPeriod>().Add(period);

        return period;
    }

    private async Task<MonthFacts> LoadMonthFactsAsync(PayrollMonth month, CancellationToken cancellationToken)
    {
        var firstDay = month.FirstDay;
        var lastDay = month.LastDay;

        // Terminated employees are loaded too when they left during the period or later: the days
        // worked before a mid-month departure still have to be paid, and that final payslip is
        // what a settlement is built on. Employee.IsPayableFor makes the final call.
        var employees = await dbContext.Set<Employee>()
            .AsNoTracking()
            .Where(employee => employee.Status != EmployeeStatus.Suspended
                && employee.HireDate <= lastDay
                && (employee.TerminationDate == null || employee.TerminationDate >= firstDay))
            .OrderBy(employee => employee.EmployeeNumber)
            .ToArrayAsync(cancellationToken);

        // Same reasoning on the contract side: the contract of a departure is Ended, not Active,
        // by the time the month is run, so filtering on Active alone would leave the leaver
        // without the contractual salary their last payslip is computed from.
        var contracts = await dbContext.Set<EmploymentContract>()
            .AsNoTracking()
            .Where(contract => contract.Status != ContractStatus.Suspended
                && contract.StartDate <= lastDay
                && (contract.TerminatedOn == null || contract.TerminatedOn >= firstDay)
                && (contract.EndDate == null || contract.EndDate >= firstDay))
            .ToArrayAsync(cancellationToken);

        // Only VALIDATED hours reach a payslip: raw or unreviewed time is evidence of presence,
        // not an agreement on what should be paid.
        var hours = await dbContext.Set<TimeEntry>()
            .AsNoTracking()
            .Where(entry => entry.WorkDate >= firstDay
                && entry.WorkDate <= lastDay
                && entry.Status == TimeEntryStatus.Validated)
            .GroupBy(entry => entry.EmployeeId)
            .Select(group => new { EmployeeId = group.Key, Hours = group.Sum(entry => entry.HoursWorked) })
            .ToDictionaryAsync(row => row.EmployeeId, row => row.Hours, cancellationToken);

        var absences = await dbContext.Set<AbsenceRequest>()
            .AsNoTracking()
            .Where(absence => absence.Status == AbsenceStatus.Approved
                && absence.StartDate <= lastDay
                && absence.EndDate >= firstDay)
            .ToArrayAsync(cancellationToken);

        var bonuses = await dbContext.Set<PayrollBonus>()
            .AsNoTracking()
            .Where(bonus => bonus.Period == month)
            .GroupBy(bonus => bonus.EmployeeId)
            .Select(group => new { EmployeeId = group.Key, Amount = group.Sum(bonus => bonus.Amount) })
            .ToDictionaryAsync(row => row.EmployeeId, row => row.Amount, cancellationToken);

        var payslips = await dbContext.Set<Payslip>()
            .Where(payslip => payslip.Period == month)
            .ToDictionaryAsync(payslip => payslip.EmployeeId, cancellationToken);

        var unpaidDays = new Dictionary<Guid, decimal>();

        foreach (var absence in absences)
        {
            // The day count is computed by the domain, which is the only place that knows an
            // absence spanning two months contributes only its days inside this one.
            var days = absence.UnpaidDaysWithin(month);

            if (days == 0)
            {
                continue;
            }

            unpaidDays[absence.EmployeeId] = unpaidDays.GetValueOrDefault(absence.EmployeeId) + days;
        }

        return new MonthFacts(
            employees,
            contracts
                .Where(contract => contract.CoversPeriod(month))
                .GroupBy(contract => contract.EmployeeId)
                // A rehire can leave two contracts covering the same month (the one that ended and
                // the new one). The still-active contract wins, then the most recent start - never
                // whichever row the database happened to return first.
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(contract => contract.IsActive)
                        .ThenByDescending(contract => contract.StartDate)
                        .First()),
            hours,
            unpaidDays,
            bonuses,
            payslips);
    }

    private async Task<PayrollTotals> SaveAndSummariseAsync(
        PayrollMonth month,
        OperationContext context,
        int generated,
        int updated,
        int skipped,
        CancellationToken cancellationToken)
    {
        await WriteAuditAsync(
            "hr.payroll.pre_payroll_generated",
            PeriodsEntity,
            Guid.Empty,
            context,
            new { Period = month.ToString(), Generated = generated, Updated = updated, Skipped = skipped },
            cancellationToken);

        return await LoadTotalsAsync(month, cancellationToken);
    }

    private async Task<PayrollTotals> LoadTotalsAsync(PayrollMonth month, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Set<Payslip>()
            .AsNoTracking()
            .Where(payslip => payslip.Period == month)
            .Select(payslip => new
            {
                payslip.TaxableGross,
                payslip.NetPay,
                payslip.EmployerCost,
                payslip.Status
            })
            .ToArrayAsync(cancellationToken);

        return new PayrollTotals(
            rows.Sum(row => row.TaxableGross),
            rows.Sum(row => row.NetPay),
            rows.Sum(row => row.EmployerCost),
            rows.Length,
            rows.Count(row => row.Status == PayslipStatus.Draft));
    }

    private async Task<string?> CheckPeriodOpenAsync(PayrollMonth month, CancellationToken cancellationToken)
    {
        var status = await dbContext.Set<PayrollPeriod>()
            .AsNoTracking()
            .Where(period => period.Period == month)
            .Select(period => (PayrollPeriodStatus?)period.Status)
            .SingleOrDefaultAsync(cancellationToken);

        return status == PayrollPeriodStatus.Closed ? ClosedPeriodMessage(month) : null;
    }

    private static string ClosedPeriodMessage(PayrollMonth month)
    {
        return $"Payroll period {month} is closed - no further modification is allowed. "
            + "Correct it with a regularisation on an open period.";
    }

    private const string InvalidPeriodMessage =
        "The payroll period must use the YYYY-MM format (for example 2026-08).";

    private async Task<PayrollPeriodResponse> MapAsync(
        PayrollPeriod period,
        CancellationToken cancellationToken)
    {
        var totals = await LoadTotalsAsync(period.Period, cancellationToken);

        return new PayrollPeriodResponse(
            period.Period.ToString(),
            period.Status,
            totals.PayslipCount,
            totals.DraftCount,
            totals.TaxableGross,
            totals.NetPay,
            totals.EmployerCost,
            period.ValidatedAt,
            period.ValidatedBy,
            period.ClosedAt,
            period.ClosedBy);
    }

    private static PayslipResponse Map(Payslip payslip, Employee employee)
    {
        return new PayslipResponse(
            payslip.Id,
            payslip.Period.ToString(),
            payslip.EmployeeId,
            employee.EmployeeNumber,
            employee.FullName,
            employee.SocialSecurityNumber,
            employee.BankAccountNumber,
            payslip.Status,
            payslip.BaseGross,
            payslip.HoursWorked,
            payslip.OvertimeHours,
            payslip.OvertimeAmount,
            payslip.UnpaidAbsenceDays,
            payslip.AbsenceDeduction,
            payslip.BonusTotal,
            payslip.TaxableGross,
            payslip.EmployeeSocialContribution,
            payslip.IncomeTaxBase,
            payslip.IncomeTax,
            payslip.NetPay,
            payslip.EmployerSocialContribution,
            payslip.EmployerPayrollTaxes,
            payslip.EmployerCost,
            payslip.BelowMinimumWage,
            payslip.ValidatedAt,
            payslip.ValidatedBy);
    }

    private static PayrollBonusResponse Map(PayrollBonus bonus, Employee employee)
    {
        return new PayrollBonusResponse(
            bonus.Id,
            bonus.Period.ToString(),
            bonus.EmployeeId,
            employee.EmployeeNumber,
            employee.FullName,
            bonus.Code,
            bonus.Label,
            bonus.Amount);
    }

    private static PayrollParameterSetResponse Map(PayrollParameterSet set)
    {
        return new PayrollParameterSetResponse(
            set.Id,
            set.EffectiveFrom.ToString(),
            set.Label,
            set.MonthlyReferenceHours,
            set.OvertimeMultiplier,
            set.ReferenceDaysPerMonth,
            set.EmployeeSocialRate,
            set.EmployerSocialRate,
            set.WorkAccidentRate,
            set.UnemploymentInsuranceRate,
            set.VocationalTrainingRate,
            set.IncomeTaxAbatement,
            set.IncomeTaxAbatementPerChild,
            set.MinimumWage,
            set.Brackets
                .OrderBy(bracket => bracket.Ordinal)
                .Select(bracket => new IncomeTaxBracketResponse(
                    bracket.Ordinal,
                    bracket.UpperBound,
                    bracket.Rate))
                .ToArray(),
            set.CreatedAt,
            set.CreatedBy);
    }

    private async Task WriteAuditAsync(
        string action,
        string entityName,
        Guid entityId,
        OperationContext context,
        object details,
        CancellationToken cancellationToken)
    {
        await auditLogWriter.WriteAsync(
            new AuditLogEntry(
                context.UserId,
                context.UserName,
                action,
                entityName,
                entityId == Guid.Empty ? null : entityId.ToString(),
                context.IpAddress,
                JsonSerializer.Serialize(details)),
            cancellationToken);
    }

    private sealed record MonthFacts(
        IReadOnlyList<Employee> Employees,
        IReadOnlyDictionary<Guid, EmploymentContract> Contracts,
        IReadOnlyDictionary<Guid, decimal> Hours,
        IReadOnlyDictionary<Guid, decimal> UnpaidDays,
        IReadOnlyDictionary<Guid, decimal> Bonuses,
        IReadOnlyDictionary<Guid, Payslip> Payslips);

    private sealed record PayrollTotals(
        decimal TaxableGross,
        decimal NetPay,
        decimal EmployerCost,
        int PayslipCount,
        int DraftCount);
}
