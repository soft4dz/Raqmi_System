namespace RaqmiSystem.Application.Channels;

/// <summary>
/// Ce que le PMS envoie au canal : le disponible COMMERCIAL par type et par nuit, tel que
/// l'AvailabilityCalculator l'a calcule. Le connecteur ne recalcule rien.
/// </summary>
public sealed record ChannelAvailabilityPush(
    string HotelUnitCode,
    DateOnly From,
    DateOnly To,
    IReadOnlyCollection<ChannelAvailabilityLine> Lines);
