namespace RaqmiSystem.Domain.HumanResources;

/// <summary>
/// Lifecycle of an employment contract. Exactly one contract per employee may be
/// <see cref="Active"/> at a time - that is the contract the pre-payroll run reads the
/// contractual gross salary from.
/// </summary>
public enum ContractStatus
{
    Active = 0,
    Suspended = 1,
    Ended = 2
}
