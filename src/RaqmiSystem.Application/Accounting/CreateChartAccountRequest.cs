using RaqmiSystem.Domain.Accounting;

namespace RaqmiSystem.Application.Accounting;

/// <summary>
/// The SCF class is not part of the request on purpose: it is the first digit of
/// <paramref name="Code"/> and is derived from it, so a code and a class cannot disagree.
/// </summary>
public sealed record CreateChartAccountRequest(
    string Code,
    string Label,
    AccountKind Kind);
