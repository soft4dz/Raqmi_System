namespace RaqmiSystem.Application.HumanResources;

/// <summary>
/// One bracket of the IRG scale. A null <see cref="UpperBound"/> marks the open-ended top
/// bracket, which must be the last one of the list.
/// </summary>
public sealed record IncomeTaxBracketRequest(
    decimal? UpperBound,
    decimal Rate);
