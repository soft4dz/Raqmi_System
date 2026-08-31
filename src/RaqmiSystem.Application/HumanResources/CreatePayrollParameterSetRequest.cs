namespace RaqmiSystem.Application.HumanResources;

/// <summary>
/// Creates the statutory parameter version applying from <see cref="EffectiveFrom"/> onwards.
/// This is how a finance act reaches the payroll engine: as data, entered once and audited,
/// never as a code change.
/// </summary>
public sealed record CreatePayrollParameterSetRequest(
    string EffectiveFrom,
    string Label,
    decimal MonthlyReferenceHours,
    decimal OvertimeMultiplier,
    int ReferenceDaysPerMonth,
    decimal EmployeeSocialRate,
    decimal EmployerSocialRate,
    decimal WorkAccidentRate,
    decimal UnemploymentInsuranceRate,
    decimal VocationalTrainingRate,
    decimal IncomeTaxAbatement,
    decimal IncomeTaxAbatementPerChild,
    decimal MinimumWage,
    IReadOnlyList<IncomeTaxBracketRequest> Brackets);
