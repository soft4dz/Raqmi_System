using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Application.HumanResources;

/// <summary>
/// Records the hours of one employee on one day. Upsert semantics: there is at most one entry
/// per employee and day, so re-sending a day corrects it instead of adding a second row.
/// </summary>
public sealed record SaveTimeEntryRequest(
    Guid EmployeeId,
    DateOnly WorkDate,
    decimal HoursWorked,
    TimeEntrySource Source);
