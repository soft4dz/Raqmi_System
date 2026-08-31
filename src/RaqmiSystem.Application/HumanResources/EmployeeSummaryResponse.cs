using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Application.HumanResources;

/// <summary>
/// The list projection of an employee. It deliberately carries NO legal identifier: the national
/// identity number, the social security number and the bank account are personal data whose
/// purpose is payroll and declarations, not browsing a directory. They are served only by the
/// detail endpoint, which is a separate authorised read and a separate audit entry.
/// </summary>
public sealed record EmployeeSummaryResponse(
    Guid Id,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string HotelUnitCode,
    string PositionCode,
    string PositionLabel,
    string DepartmentCode,
    EmployeeStatus Status,
    DateOnly HireDate,
    DateOnly? TerminationDate,
    decimal? ActiveContractGrossSalary);
