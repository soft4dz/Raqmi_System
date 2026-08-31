namespace RaqmiSystem.Application.HumanResources;

public sealed record TerminateEmployeeRequest(
    DateOnly TerminationDate,
    string Reason);
