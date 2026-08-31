namespace RaqmiSystem.Domain.Mice;

/// <summary>
/// Cycle de vie d'un allotement.
///
/// <see cref="Draft"/> TIENT DEJA les chambres, comme <see cref="Confirmed"/> : une option posee
/// pour un groupe retire ces chambres de la vente publique, sinon deux commerciaux vendraient le
/// meme inventaire. Seuls <see cref="Released"/> et <see cref="Cancelled"/> les rendent.
/// </summary>
public enum RoomAllotmentStatus
{
    /// <summary>Option posee pour le groupe. Tient les chambres.</summary>
    Draft = 0,

    /// <summary>Groupe confirme. Tient les chambres.</summary>
    Confirmed = 1,

    /// <summary>
    /// Reliquat rendu a la vente avant terme, par decision commerciale. Les chambres deja prises
    /// sur le bloc restent reservees : liberer un allotement rend le SOLDE, pas les nuitees vendues.
    /// </summary>
    Released = 2,

    /// <summary>Annule : le bloc entier retourne a la vente. Motif obligatoire.</summary>
    Cancelled = 3
}
