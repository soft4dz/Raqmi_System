namespace RaqmiSystem.Domain.Accounting;

/// <summary>
/// One of the seven SCF account classes: its number, its official heading, and the account kinds
/// an account of that class may legitimately carry.
/// </summary>
public sealed record AccountClassDefinition(
    int AccountClass,
    string Label,
    IReadOnlyCollection<AccountKind> AllowedKinds);
