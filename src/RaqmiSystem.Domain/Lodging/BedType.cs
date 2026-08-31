namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// Nature d'un couchage FIXE d'une chambre. Les lits d'appoint et les berceaux n'en font pas
/// partie : ils s'ajoutent au couchage fixe et sont comptes separement, parce qu'ils ne sont pas
/// installes en permanence et qu'ils se facturent differemment.
///
/// Chaque nature porte le nombre de personnes qu'elle couche (<see cref="BedTypes.Sleeps"/>). Cette
/// correspondance est le seul endroit du produit ou "un lit double couche deux personnes" est
/// ecrit : tout le reste s'en deduit.
/// </summary>
public enum BedType
{
    /// <summary>Lit simple, une personne.</summary>
    Single = 0,

    /// <summary>Lit double standard, deux personnes.</summary>
    Double = 1,

    /// <summary>Queen size, deux personnes.</summary>
    Queen = 2,

    /// <summary>King size, deux personnes.</summary>
    King = 3,

    /// <summary>Canape-lit installe en permanence, deux personnes.</summary>
    SofaBed = 4,

    /// <summary>Lits superposes, deux personnes.</summary>
    BunkBed = 5
}

/// <summary>Correspondance entre une nature de couchage et le nombre de personnes couchees.</summary>
public static class BedTypes
{
    /// <summary>
    /// Nombre de personnes qu'un exemplaire de ce lit couche. Un canape-lit compte pour deux comme
    /// un lit double : s'il ne sert qu'occasionnellement, ce n'est pas un couchage fixe mais un lit
    /// d'appoint, et il se declare comme tel.
    /// </summary>
    public static int Sleeps(this BedType bedType) => bedType switch
    {
        BedType.Single => 1,
        BedType.Double => 2,
        BedType.Queen => 2,
        BedType.King => 2,
        BedType.SofaBed => 2,
        BedType.BunkBed => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(bedType), bedType, "Nature de couchage inconnue.")
    };
}
