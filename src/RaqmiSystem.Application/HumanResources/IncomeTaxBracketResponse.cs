namespace RaqmiSystem.Application.HumanResources;

public sealed record IncomeTaxBracketResponse(
    int Ordinal,
    decimal? UpperBound,
    decimal Rate);
