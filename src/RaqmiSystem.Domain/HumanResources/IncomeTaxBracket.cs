namespace RaqmiSystem.Domain.HumanResources;

/// <summary>
/// One bracket of the progressive IRG (impot sur le revenu global) scale, expressed as a marginal
/// rate applied to the fraction of the taxable base that falls inside the bracket.
///
/// The published scale is usually written with cumulative fixed amounts ("6 900 DZD + 27% above
/// 30 000"). Those fixed amounts are DERIVED - 6 900 is 23% of the first 30 000 - so they are
/// deliberately not stored: keeping both would let a rate change update one and not the other,
/// producing a scale that no longer adds up. The engine recomputes them by walking the brackets
/// in order.
/// </summary>
/// <param name="UpperBound">
/// Inclusive upper bound of the bracket, or null for the open-ended top bracket. Exactly one
/// bracket of a set may be open-ended and it must be the last one.
/// </param>
/// <param name="Rate">Marginal rate as a fraction (0.23 = 23%).</param>
public sealed record IncomeTaxBracket(decimal? UpperBound, decimal Rate)
{
    public static IncomeTaxBracket Create(decimal? upperBound, decimal rate)
    {
        if (upperBound is <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(upperBound),
                "An income tax bracket upper bound must be greater than zero.");
        }

        if (rate is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rate),
                "An income tax rate must be a fraction between 0 and 1.");
        }

        return new IncomeTaxBracket(upperBound, rate);
    }
}
