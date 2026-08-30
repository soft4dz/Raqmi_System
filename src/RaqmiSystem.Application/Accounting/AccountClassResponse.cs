using RaqmiSystem.Domain.Accounting;

namespace RaqmiSystem.Application.Accounting;

/// <summary>
/// One SCF account class as exposed by <c>GET /accounting/account-classes</c>: the reference a
/// chart-of-accounts screen needs to label classes and to offer only the account kinds that a
/// given class accepts.
/// </summary>
public sealed record AccountClassResponse(
    int AccountClass,
    string Label,
    IReadOnlyCollection<AccountKind> AllowedKinds);
