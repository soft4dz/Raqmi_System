namespace RaqmiSystem.Application.HumanResources;

public sealed record PayrollParameterSetResponse(
    Guid Id,
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
    IReadOnlyCollection<IncomeTaxBracketResponse> Brackets,
    DateTimeOffset CreatedAt,
    string CreatedBy);
