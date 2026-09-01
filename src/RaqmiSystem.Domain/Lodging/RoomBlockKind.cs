namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// Nature d'une indisponibilite de chambre. La distinction n'est PAS cosmetique : elle commande
/// l'inventaire vendable, et c'est pour cela qu'elle ne peut pas rester un simple motif libre.
/// </summary>
public enum RoomBlockKind
{
    /// <summary>
    /// Hors service technique (panne, degat des eaux, travaux lourds). La chambre sort de
    /// l'inventaire vendable, toujours et sans exception : elle n'est pas louable, quelle que
    /// soit la politique de l'hotel.
    /// </summary>
    OutOfOrder = 0,

    /// <summary>
    /// Indisponibilite d'exploitation (nettoyage approfondi, usage interne, blocage
    /// administratif, retouche legere). La chambre existe, elle est en etat, mais elle n'est pas
    /// a vendre pour le moment.
    ///
    /// Son effet sur l'inventaire COMMERCIAL est parametrable par unite
    /// (<see cref="LodgingPolicy.OutOfServiceReducesInventory"/>) : certains hotels comptent ces
    /// chambres comme vendables et assument de deplacer l'usage interne si un client se presente,
    /// d'autres non. Le taux d'occupation, lui, en tient toujours compte.
    /// </summary>
    OutOfService = 1
}
