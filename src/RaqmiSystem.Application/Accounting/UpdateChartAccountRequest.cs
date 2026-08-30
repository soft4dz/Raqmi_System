using RaqmiSystem.Domain.Accounting;

namespace RaqmiSystem.Application.Accounting;

/// <summary>
/// The code is absent because it is immutable: it carries the account's class and every posted
/// line already recorded against it references it.
/// </summary>
public sealed record UpdateChartAccountRequest(
    string Label,
    AccountKind Kind);
