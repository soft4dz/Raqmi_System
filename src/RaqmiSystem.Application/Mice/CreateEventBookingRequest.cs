namespace RaqmiSystem.Application.Mice;

/// <summary>
/// Books a function space for an event.
///
/// <paramref name="SetupMinutes"/> and <paramref name="TeardownMinutes"/> are not decoration: they
/// extend the window during which the space is genuinely unavailable, and the double-booking guard
/// compares that extended window. Leaving them at zero means claiming the room can be flipped
/// between two events instantly.
/// </summary>
public sealed record CreateEventBookingRequest(
    string HotelUnitCode,
    string Reference,
    string FunctionSpaceCode,
    string CustomerCode,
    string Title,
    DateOnly EventDate,
    TimeOnly StartTime,
    int DurationMinutes,
    int SetupMinutes,
    int TeardownMinutes,
    string SetupStyle,
    int ExpectedAttendance,
    string? Notes);
