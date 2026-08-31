using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Application.HumanResources;

public sealed record PayrollPeriodResponse(
    string Period,
    PayrollPeriodStatus Status,
    int PayslipCount,
    int DraftPayslipCount,
    decimal TotalTaxableGross,
    decimal TotalNetPay,
    decimal TotalEmployerCost,
    DateTimeOffset? ValidatedAt,
    string? ValidatedBy,
    DateTimeOffset? ClosedAt,
    string? ClosedBy);
