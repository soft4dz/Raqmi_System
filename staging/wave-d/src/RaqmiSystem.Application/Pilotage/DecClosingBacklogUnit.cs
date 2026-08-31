namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// One hotel unit's closing backlog: business days STRICTLY BEFORE YESTERDAY that are not
/// closed (no DailyClosing row at the Closed status - a Reopened day counts as not closed,
/// it must be closed again). Yesterday itself is today's normal work and is reported in the
/// unit-health table, not here.
/// </summary>
public sealed record DecClosingBacklogUnit(
    string HotelUnitCode,
    string? HotelUnitName,
    IReadOnlyCollection<DateOnly> MissingDates,
    DateOnly OldestMissingDate,
    int OldestAgeDays);
