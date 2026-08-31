using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Application.HumanResources;

/// <summary>
/// Full employee file, legal identifiers included. Every read of this projection is audited -
/// see the HR service - because it exposes the personal data protected by law 18-07.
/// </summary>
public sealed record EmployeeResponse(
    Guid Id,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string HotelUnitCode,
    string PositionCode,
    string PositionLabel,
    string DepartmentCode,
    string DepartmentLabel,
    EmployeeStatus Status,
    DateOnly HireDate,
    DateOnly? TerminationDate,
    string? Email,
    string? Phone,
    string? NationalIdentityNumber,
    string? SocialSecurityNumber,
    string? BankAccountNumber,
    string? BadgeId,
    int DependentChildren,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
