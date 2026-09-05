namespace RaqmiSystem.Application.Security;

/// <summary>
/// Implementation immuable de <see cref="IUnitScope"/>. Deux fabriques, pour que le code
/// appelant dise ce qu'il veut dire : <see cref="Global"/> ou <see cref="Restricted"/>.
/// </summary>
public sealed class UnitScope : IUnitScope
{
    private readonly HashSet<string> _codes;

    private UnitScope(bool isGlobal, IEnumerable<string> codes)
    {
        IsGlobal = isGlobal;
        _codes = codes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(Normalize)
            .ToHashSet(StringComparer.Ordinal);
        AllowedUnitCodes = _codes.Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>Le perimetre de tout compte sans affectation : il voit toutes les unites.</summary>
    public static UnitScope Global { get; } = new(isGlobal: true, []);

    /// <summary>
    /// Un perimetre restreint aux codes donnes. Une liste vide est legitime et signifie « aucune
    /// unite » (toutes les affectations du compte sont hors validite) : ce n'est PAS un global.
    /// </summary>
    public static UnitScope Restricted(IEnumerable<string> hotelUnitCodes)
    {
        ArgumentNullException.ThrowIfNull(hotelUnitCodes);

        return new UnitScope(isGlobal: false, hotelUnitCodes);
    }

    public bool IsGlobal { get; }

    public IReadOnlyCollection<string> AllowedUnitCodes { get; }

    public bool Allows(string? hotelUnitCode)
    {
        if (IsGlobal)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(hotelUnitCode) && _codes.Contains(Normalize(hotelUnitCode));
    }

    /// <summary>
    /// Meme normalisation que <c>HotelUnit.NormalizeCode</c> (majuscules, sans espaces), sans
    /// ses controles de longueur : un code lu dans un jeton ou une requete n'a pas a lever une
    /// exception, il a a etre compare.
    /// </summary>
    private static string Normalize(string value)
    {
        return value.Trim().ToUpperInvariant();
    }
}
