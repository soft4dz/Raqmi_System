namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// Cycle de vie d'une reservation.
///
/// CE QUI SEPARE CES ETATS N'EST PAS LEUR NOM, C'EST L'INVENTAIRE. Trois familles :
///   - <see cref="Inquiry"/> ne tient RIEN : une demande n'immobilise pas une chambre, sans quoi
///     un formulaire web ferait fermer l'hotel ;
///   - <see cref="Option"/>, <see cref="Confirmed"/>, <see cref="Guaranteed"/>,
///     <see cref="CheckedIn"/> et <see cref="CheckedOut"/> TIENNENT la chambre - une option posee
///     retire la chambre de la vente, sinon deux commerciaux vendraient le meme inventaire, et un
///     sejour termine garde ses nuits, qui ont bien ete consommees ;
///   - <see cref="Cancelled"/> et <see cref="NoShow"/> rendent la chambre.
///
/// Cette famille est lue en un seul endroit, <c>Reservation.IsBlocking</c>, dont depend la garde
/// anti-double-reservation.
/// </summary>
public enum ReservationStatus
{
    /// <summary>
    /// Demande enregistree, sans engagement : ne tient pas la chambre et n'apparait pas dans
    /// l'occupation. C'est l'etat d'une demande de groupe ou d'un formulaire en attente de
    /// reponse commerciale.
    /// </summary>
    Inquiry = 0,

    /// <summary>
    /// Option posee : la chambre est tenue, mais l'accord n'est pas ferme. Tient l'inventaire -
    /// c'est precisement pour cela qu'on pose une option.
    /// </summary>
    Option = 1,

    /// <summary>Reservation ferme, sans garantie financiere. Tient l'inventaire.</summary>
    Confirmed = 2,

    /// <summary>
    /// Reservation garantie (carte, acompte, prise en charge, voucher). Tient l'inventaire et
    /// autorise l'application d'une penalite en cas de no-show.
    /// </summary>
    Guaranteed = 3,

    /// <summary>Le client est dans la chambre ; le folio est ouvert.</summary>
    CheckedIn = 4,

    /// <summary>Le sejour est termine et le folio a ete solde.</summary>
    CheckedOut = 5,

    /// <summary>Annulee avant arrivee, motif obligatoire. Libere la chambre.</summary>
    Cancelled = 6,

    /// <summary>Le client ne s'est pas presente. Libere la chambre.</summary>
    NoShow = 7
}

/// <summary>Lectures partagees du statut, pour ne pas repeter les memes ensembles a dix endroits.</summary>
public static class ReservationStatuses
{
    /// <summary>
    /// Les statuts qui TIENNENT la chambre. Tout sauf la simple demande, l'annulation et le
    /// no-show. C'est l'unique definition de "chambre occupee" du produit.
    /// </summary>
    public static bool Blocks(this ReservationStatus status)
    {
        return status is ReservationStatus.Option
            or ReservationStatus.Confirmed
            or ReservationStatus.Guaranteed
            or ReservationStatus.CheckedIn
            or ReservationStatus.CheckedOut;
    }

    /// <summary>
    /// Les statuts d'AVANT ARRIVEE : ceux depuis lesquels on peut encore enregistrer l'arrivee,
    /// annuler, constater un no-show, deplacer les dates ou changer de type.
    /// </summary>
    public static bool IsPreArrival(this ReservationStatus status)
    {
        return status is ReservationStatus.Inquiry
            or ReservationStatus.Option
            or ReservationStatus.Confirmed
            or ReservationStatus.Guaranteed;
    }

    /// <summary>Les statuts fermes : plus aucune transition n'en part.</summary>
    public static bool IsClosed(this ReservationStatus status)
    {
        return status is ReservationStatus.CheckedOut
            or ReservationStatus.Cancelled
            or ReservationStatus.NoShow;
    }
}
