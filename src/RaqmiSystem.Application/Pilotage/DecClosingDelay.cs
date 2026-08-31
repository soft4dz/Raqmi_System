namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// The group's single oldest closing delay: the oldest unclosed business day (before yesterday)
/// across every unit, with its age in days. Null on the response when there is no backlog.
/// </summary>
public sealed record DecClosingDelay(
    string HotelUnitCode,
    string? HotelUnitName,
    DateOnly BusinessDate,
    int AgeDays);
