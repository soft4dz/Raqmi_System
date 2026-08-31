namespace RaqmiSystem.Application.HumanResources;

public sealed record UpdateEmployeeRequest(
    string FirstName,
    string LastName,
    string HotelUnitCode,
    string PositionCode,
    string? Email,
    string? Phone,
    string? NationalIdentityNumber,
    string? SocialSecurityNumber,
    string? BankAccountNumber,
    string? BadgeId,
    int DependentChildren);
