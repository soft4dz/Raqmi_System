namespace RaqmiSystem.Application.Mice;

/// <summary>
/// Moves an event in time, and optionally into another space. The whole slot is restated rather
/// than patched field by field, because the occupied window is derived from all of these values at
/// once: a partial update would let the guard run against a half-old slot.
/// </summary>
public sealed record RescheduleEventBookingRequest(
    string FunctionSpaceCode,
    DateOnly EventDate,
    TimeOnly StartTime,
    int DurationMinutes,
    int SetupMinutes,
    int TeardownMinutes);
