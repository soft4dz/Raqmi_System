namespace RaqmiSystem.Application.Mice;

/// <summary>
/// Un bloc de chambres tenu pour un groupe.
///
/// <paramref name="PickedUpPeak"/> est le nombre de chambres du bloc effectivement prises la nuit
/// la plus chargee, et <paramref name="RemainingAtPeak"/> ce qu'il en reste ce soir-la. Ce sont ces
/// deux chiffres qui disent si le groupe consomme son bloc ou s'il faut songer a le reduire : une
/// moyenne masquerait exactement le soir ou tout est pris.
///
/// <paramref name="IsHolding"/> dit si le bloc retire ENCORE des chambres de la vente publique a la
/// date du jour. Il passe a faux des que la date de release est franchie, sans qu'aucune
/// reservation n'ait bouge.
/// </summary>
public sealed record RoomAllotmentResponse(
    Guid Id,
    string HotelUnitCode,
    string Reference,
    string CustomerCode,
    string CustomerName,
    string RoomTypeCode,
    string RoomTypeLabel,
    DateOnly ArrivalDate,
    DateOnly DepartureDate,
    int Nights,
    int RoomsHeld,
    DateOnly? ReleaseDate,
    string Status,
    bool IsHolding,
    int PickedUpPeak,
    int RemainingAtPeak,
    string? Notes,
    string? CancelReason);
