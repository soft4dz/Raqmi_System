namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Result of an availability search over [From, To) for one hotel unit: the active rooms whose
/// type can host the party and that no blocking reservation overlaps, each with its per-night
/// pricing (see <see cref="AvailableRoomResponse"/>). This is the dates-first booking flow of a
/// PMS: pick the dates, see every bookable room priced, then reserve one.
/// </summary>
public sealed record AvailabilityResponse(
    string HotelUnitCode,
    DateOnly From,
    DateOnly To,
    int Nights,
    int Guests,
    IReadOnlyCollection<AvailableRoomResponse> Rooms);
