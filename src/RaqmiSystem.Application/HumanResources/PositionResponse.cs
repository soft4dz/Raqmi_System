namespace RaqmiSystem.Application.HumanResources;

public sealed record PositionResponse(
    Guid Id,
    string Code,
    string Label,
    string DepartmentCode,
    string DepartmentLabel,
    decimal MinimumGrossSalary,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
