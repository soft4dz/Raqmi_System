namespace RaqmiSystem.Domain.Lodging;

/// <summary>Etat d'un folio.</summary>
public enum FolioStatus
{
    /// <summary>Ouvert : il accepte des lignes.</summary>
    Open = 0,

    /// <summary>
    /// Solde et ferme. Il n'accepte plus de ligne : une correction posterieure passe par un avoir
    /// du module Facturation, pas par une reecriture du folio.
    /// </summary>
    Closed = 1
}
