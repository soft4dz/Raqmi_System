namespace RaqmiSystem.Application.HumanResources;

public sealed record PayrollBonusResponse(
    Guid Id,
    string Period,
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeFullName,
    string Code,
    string Label,
    decimal Amount);
