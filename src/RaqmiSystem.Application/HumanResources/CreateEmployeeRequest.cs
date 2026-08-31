namespace RaqmiSystem.Application.HumanResources;

public sealed record CreateEmployeeRequest(
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string HotelUnitCode,
    string PositionCode,
    DateOnly HireDate,
    string? Email,
    string? Phone,
    string? NationalIdentityNumber,
    string? SocialSecurityNumber,
    string? BankAccountNumber,
    string? BadgeId,
    int DependentChildren);
