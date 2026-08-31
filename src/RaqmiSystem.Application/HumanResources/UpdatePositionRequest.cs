namespace RaqmiSystem.Application.HumanResources;

public sealed record UpdatePositionRequest(
    string Label,
    string DepartmentCode,
    decimal MinimumGrossSalary);
