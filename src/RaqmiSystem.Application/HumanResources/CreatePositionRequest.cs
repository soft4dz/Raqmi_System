namespace RaqmiSystem.Application.HumanResources;

public sealed record CreatePositionRequest(
    string Code,
    string Label,
    string DepartmentCode,
    decimal MinimumGrossSalary);
