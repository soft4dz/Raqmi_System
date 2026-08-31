using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Application.HumanResources;

public sealed record AbsenceResponse(
    Guid Id,
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeFullName,
    AbsenceType Type,
    bool IsUnpaid,
    DateOnly StartDate,
    DateOnly EndDate,
    int TotalDays,
    string? Reason,
    AbsenceStatus Status,
    DateTimeOffset? DecidedAt,
    string? DecidedBy,
    string? DecisionNote);
