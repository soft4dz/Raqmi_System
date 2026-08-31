using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Application.HumanResources;

public sealed record CreateContractRequest(
    ContractType Type,
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal GrossSalary,
    decimal WeeklyHours);
