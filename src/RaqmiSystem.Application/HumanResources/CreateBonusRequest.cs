namespace RaqmiSystem.Application.HumanResources;

public sealed record CreateBonusRequest(
    Guid EmployeeId,
    string Code,
    string Label,
    decimal Amount);
