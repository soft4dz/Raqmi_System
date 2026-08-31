namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Creation d'une reservation.
///
/// <paramref name="AllotmentId"/> distingue les deux natures de vente, et cette distinction
/// commande tout le controle de disponibilite :
///   * null : vente PUBLIQUE. Elle ne doit pas entamer les chambres tenues pour un groupe ; le
///     service refuse si la prendre passerait sous le solde d'un allotement.
///   * renseigne : vente SUR UN BLOC. Elle consomme le bloc, qui avait deja retire la chambre de
///     la vente publique - la compter une seconde fois interdirait de vendre des chambres libres.
///
/// <paramref name="GuestName"/> est le nom de l'occupant sur la rooming list d'un groupe. Null pour
/// une vente publique, ou tant que le groupe n'a pas transmis ses noms.
/// </summary>
public sealed record CreateReservationRequest(
    string HotelUnitCode,
    Guid RoomId,
    string CustomerCode,
    DateOnly ArrivalDate,
    DateOnly DepartureDate,
    int GuestCount,
    Guid? AllotmentId = null,
    string? GuestName = null);
