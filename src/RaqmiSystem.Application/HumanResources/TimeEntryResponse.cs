using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Application.HumanResources;

public sealed record TimeEntryResponse(
    Guid Id,
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeFullName,
    DateOnly WorkDate,
    decimal HoursWorked,
    TimeEntrySource Source,
    TimeEntryStatus Status,
    DateTimeOffset? ValidatedAt,
    string? ValidatedBy);
