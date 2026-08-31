using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;

namespace RaqmiSystem.Application.HumanResources;

/// <summary>
/// Payroll side of the HR module: statutory parameter versions, variable pay elements, the
/// monthly pre-payroll run, payslips, and the validation then closing of a period.
///
/// The normal month runs in this order: capture time and absences (see
/// <see cref="IHumanResourcesService"/>), add the bonuses, generate the pre-payroll as many
/// times as needed, validate the payslips one by one, validate the period, then close it. Only
/// the last step is irreversible.
///
/// Every period is addressed by its "YYYY-MM" text form. An unparsable period is a validation
/// error, never a silently empty result.
/// </summary>
public interface IPayrollService
{
    Task<IReadOnlyCollection<PayrollParameterSetResponse>> ListParameterSetsAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Registers the statutory parameters applying from a given period onwards. This is the
    /// supported way to follow a finance act: no rate, abatement, scale or minimum wage is
    /// compiled into the engine.
    /// </summary>
    Task<ApplicationResult<PayrollParameterSetResponse>> CreateParameterSetAsync(
        CreatePayrollParameterSetRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PayrollPeriodResponse>> ListPeriodsAsync(CancellationToken cancellationToken);

    Task<ApplicationResult<PayrollPeriodResponse>> GetPeriodAsync(
        string period,
        CancellationToken cancellationToken);

    Task<ApplicationResult<IReadOnlyCollection<PayrollBonusResponse>>> ListBonusesAsync(
        string period,
        CancellationToken cancellationToken);

    Task<ApplicationResult<PayrollBonusResponse>> AddBonusAsync(
        string period,
        CreateBonusRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<PayrollBonusResponse>> DeleteBonusAsync(
        string period,
        Guid bonusId,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Computes the payslips of the period for every payable employee holding an active contract.
    /// Idempotent by design: draft payslips are recomputed, validated ones are left untouched and
    /// reported as skipped, so the run can be repeated after any correction without ever
    /// rewriting a figure that has already been signed off.
    /// </summary>
    Task<ApplicationResult<PrePayrollRunResponse>> GeneratePrePayrollAsync(
        string period,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<IReadOnlyCollection<PayslipResponse>>> ListPayslipsAsync(
        string period,
        Guid? employeeId,
        CancellationToken cancellationToken);

    Task<ApplicationResult<PayslipResponse>> ValidatePayslipAsync(
        string period,
        Guid payslipId,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Marks the period as fully reviewed. Refused while any payslip is still a draft, with the
    /// count in the message - the same guard the legacy system applied, kept because it is the
    /// only thing standing between a forgotten payslip and a closed month.
    /// </summary>
    Task<ApplicationResult<PayrollPeriodResponse>> ValidatePeriodAsync(
        string period,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Closes the period for good. After this, no payslip, bonus, time entry or absence of that
    /// month can be written again. One-way on purpose: a closed month is corrected by a
    /// regularisation on an open one.
    /// </summary>
    Task<ApplicationResult<PayrollPeriodResponse>> ClosePeriodAsync(
        string period,
        OperationContext context,
        CancellationToken cancellationToken);
}
