namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Resultat d'une recherche de disponibilite sur [From, To) pour une unite.
///
/// DEUX NIVEAUX DE LECTURE, ET C'EST VOULU. <paramref name="RoomTypes"/> repond a la question
/// commerciale - "que puis-je vendre, a quel prix, sur toute la periode" - et c'est le niveau
/// auquel un PMS vend : le client achete une double standard, pas la 214.
/// <paramref name="Rooms"/> repond a la question d'exploitation - "quelles chambres physiques sont
/// libres" - et sert a l'affectation, jamais a la vente.
///
/// <paramref name="RestrictionMessages"/> porte les regles qui ferment la periode independamment de
/// tout type (stop sell d'hotel, CTA, CTD, duree minimale) : sans elles, un ecran vide laisserait
/// croire a une occupation complete alors que la vente est simplement fermee.
/// </summary>
public sealed record AvailabilityResponse(
    string HotelUnitCode,
    DateOnly From,
    DateOnly To,
    int Nights,
    int Guests,
    IReadOnlyCollection<AvailableRoomResponse> Rooms,
    IReadOnlyCollection<RoomTypeAvailabilityResponse>? RoomTypes = null,
    IReadOnlyCollection<string>? RestrictionMessages = null,
    bool IsClosed = false);
