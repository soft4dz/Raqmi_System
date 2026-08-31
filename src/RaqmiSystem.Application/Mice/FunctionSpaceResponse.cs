namespace RaqmiSystem.Application.Mice;

/// <summary>
/// A bookable function space. <paramref name="MaxAttendance"/> is the largest party the room can
/// take, all layouts considered - a single figure per space is a stated simplification of this
/// version, see EventSetupStyle.
/// </summary>
public sealed record FunctionSpaceResponse(
    string HotelUnitCode,
    string Code,
    string Label,
    int MaxAttendance,
    decimal? AreaSquareMeters,
    string? Notes,
    bool IsActive,
    int UpcomingEventCount);
