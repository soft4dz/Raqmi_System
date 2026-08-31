using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Application.HumanResources;

public sealed record CreateAbsenceRequest(
    Guid EmployeeId,
    AbsenceType Type,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Reason);
