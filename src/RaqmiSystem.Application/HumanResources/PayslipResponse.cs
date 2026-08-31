using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Application.HumanResources;

/// <summary>
/// One payslip, line by line as printed. The identities the reader can check by hand hold on
/// these figures: TaxableGross = BaseGross + OvertimeAmount + BonusTotal - AbsenceDeduction, and
/// NetPay = TaxableGross - EmployeeSocialContribution - IncomeTax.
/// </summary>
public sealed record PayslipResponse(
    Guid Id,
    string Period,
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeFullName,
    string? SocialSecurityNumber,
    string? BankAccountNumber,
    PayslipStatus Status,
    decimal BaseGross,
    decimal HoursWorked,
    decimal OvertimeHours,
    decimal OvertimeAmount,
    decimal UnpaidAbsenceDays,
    decimal AbsenceDeduction,
    decimal BonusTotal,
    decimal TaxableGross,
    decimal EmployeeSocialContribution,
    decimal IncomeTaxBase,
    decimal IncomeTax,
    decimal NetPay,
    decimal EmployerSocialContribution,
    decimal EmployerPayrollTaxes,
    decimal EmployerCost,
    bool BelowMinimumWage,
    DateTimeOffset? ValidatedAt,
    string? ValidatedBy);
