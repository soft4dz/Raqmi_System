namespace RaqmiSystem.Application.Mice;

/// <summary>
/// Descriptive change to an event: what it is called, how the room is laid out, how many people
/// are expected. The slot is NOT changed here - moving an event has to re-run the double-booking
/// guard, so it has its own operation.
/// </summary>
public sealed record UpdateEventBookingRequest(
    string Title,
    string SetupStyle,
    int ExpectedAttendance,
    string? Notes);
