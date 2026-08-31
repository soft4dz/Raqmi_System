namespace RaqmiSystem.Application.HumanResources;

public sealed record DepartmentResponse(
    Guid Id,
    string Code,
    string Label,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
