namespace RaqmiSystem.Application.Mice;

/// <summary>
/// One event with everything needed to print its BEO: the slot, the layout, the priced lines and
/// the running order.
///
/// <paramref name="OccupiedFrom"/> and <paramref name="OccupiedTo"/> are the REAL occupation of the
/// space, setup and teardown included. They are returned because they, and not the guest-facing
/// times, are what the planning must show: a room that looks free between two events at 18:00 may
/// in fact still be occupied by a teardown.
/// </summary>
public sealed record EventBookingResponse(
    Guid Id,
    string HotelUnitCode,
    string Reference,
    string FunctionSpaceCode,
    string FunctionSpaceLabel,
    string CustomerCode,
    string CustomerName,
    string Title,
    DateOnly EventDate,
    TimeOnly StartTime,
    int DurationMinutes,
    int SetupMinutes,
    int TeardownMinutes,
    DateTime OccupiedFrom,
    DateTime OccupiedTo,
    string SetupStyle,
    int ExpectedAttendance,
    int SpaceMaxAttendance,
    string Status,
    string? Notes,
    string? CancelReason,
    Guid? InvoiceId,
    string? InvoiceNumber,
    decimal TotalExclVat,
    decimal TotalVat,
    decimal TotalInclVat,
    IReadOnlyCollection<EventBookingLineResponse> Lines,
    IReadOnlyCollection<EventScheduleItemResponse> Schedule);
