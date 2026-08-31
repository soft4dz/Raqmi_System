namespace RaqmiSystem.Application.HumanResources;

public sealed record UpdateContractRequest(
    decimal GrossSalary,
    decimal WeeklyHours,
    DateOnly? EndDate);
