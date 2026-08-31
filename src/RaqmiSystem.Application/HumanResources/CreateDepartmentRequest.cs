namespace RaqmiSystem.Application.HumanResources;

public sealed record CreateDepartmentRequest(
    string Code,
    string Label);
