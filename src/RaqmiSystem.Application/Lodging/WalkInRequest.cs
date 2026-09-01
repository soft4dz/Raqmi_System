using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Un walk-in : le client est au comptoir. La vente, l'affectation de la chambre et l'arrivee se
/// font dans le meme geste.
///
/// POURQUOI UN CHEMIN A PART ET PAS UNE RESERVATION SUIVIE D'UN CHECK-IN. Les deux gestes doivent
/// reussir ou echouer ENSEMBLE : un dossier cree puis un check-in refuse laisserait une reservation
/// fantome sur une chambre que le client n'occupe pas, et personne au comptoir n'irait la nettoyer.
/// La chambre est donc obligatoire ici, contrairement a une reservation ordinaire.
/// </summary>
public sealed record WalkInRequest(
    string HotelUnitCode,
    Guid RoomId,
    string CustomerCode,
    DateOnly DepartureDate,
    int Adults,
    int Children = 0,
    int Infants = 0,
    string? GuestName = null,
    string? MarketSegmentCode = null,
    string? ChannelCode = null,
    string? SourceCode = null,
    string? Notes = null,
    string? SpecialRequests = null,
    GuaranteeKind Guarantee = GuaranteeKind.None,
    string? GuaranteeReference = null,
    bool AllowOverbooking = false,
    bool OverrideRestrictions = false);
