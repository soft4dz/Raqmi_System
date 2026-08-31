namespace RaqmiSystem.Domain.HumanResources;

/// <summary>
/// Persisted form of one <see cref="IncomeTaxBracket"/>, owned by a
/// <see cref="PayrollParameterSet"/>. <see cref="Ordinal"/> carries the position in the scale:
/// a progressive scale read out of order taxes the wrong fractions, and relying on the natural
/// order of rows returned by a database would leave that correctness to chance.
/// </summary>
public sealed class PayrollTaxBracket
{
    private PayrollTaxBracket()
    {
    }

    public PayrollTaxBracket(int ordinal, decimal? upperBound, decimal rate)
    {
        if (ordinal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal), "Bracket ordinal cannot be negative.");
        }

        var bracket = IncomeTaxBracket.Create(upperBound, rate);

        Ordinal = ordinal;
        UpperBound = bracket.UpperBound;
        Rate = bracket.Rate;
    }

    public int Ordinal { get; private set; }

    /// <summary>Inclusive upper bound, or null for the open-ended top bracket.</summary>
    public decimal? UpperBound { get; private set; }

    public decimal Rate { get; private set; }

    public IncomeTaxBracket ToBracket()
    {
        return new IncomeTaxBracket(UpperBound, Rate);
    }
}
