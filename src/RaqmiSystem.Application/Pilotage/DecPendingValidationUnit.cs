namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// One hotel unit's backlog of SUBMITTED daily revenue entries awaiting validation - the DEC's
/// number-one daily action. The oldest entry is surfaced with its age so the queue can be worked
/// oldest-first.
/// </summary>
public sealed record DecPendingValidationUnit(
    string HotelUnitCode,
    string? HotelUnitName,
    int Count,
    decimal TotalAmount,
    DateOnly OldestBusinessDate,
    DateTimeOffset? OldestSubmittedAt,
    int OldestAgeDays);
