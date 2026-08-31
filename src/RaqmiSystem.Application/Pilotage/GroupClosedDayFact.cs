using RaqmiSystem.Domain.Closing;

namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// One daily closing of the period, with its status: only a closing at status Closed counts as
/// a closed day for the unclosed-days figure - a Reopened day is, by definition, open again.
/// </summary>
public sealed record GroupClosedDayFact(
    string HotelUnitCode,
    DateOnly BusinessDate,
    ClosingStatus Status);
