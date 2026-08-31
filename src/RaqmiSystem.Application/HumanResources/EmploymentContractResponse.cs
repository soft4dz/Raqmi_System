using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Application.HumanResources;

public sealed record EmploymentContractResponse(
    Guid Id,
    Guid EmployeeId,
    ContractType Type,
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal GrossSalary,
    decimal WeeklyHours,
    ContractStatus Status,
    DateOnly? TerminatedOn,
    string? TerminationReason,
    bool BelowPositionFloor,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
