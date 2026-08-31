namespace RaqmiSystem.Application.Mice;

/// <summary>
/// One line of the BEO running order. <paramref name="StartTime"/> is the hotel wall clock, not
/// UTC: this document is read by people standing in the building.
/// </summary>
public sealed record EventScheduleItemRequest(
    TimeOnly StartTime,
    string Description,
    string? Department);
