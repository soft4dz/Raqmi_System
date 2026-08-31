namespace RaqmiSystem.Application.HumanResources;

public sealed record EndContractRequest(
    DateOnly TerminatedOn,
    string Reason);
