namespace RaqmiSystem.Application.Mice;

/// <summary>
/// Creation or update of a function space. On update the code and the unit are taken from the
/// route, not from this payload: renaming a space's key would silently orphan the events already
/// pointing at it.
/// </summary>
public sealed record SaveFunctionSpaceRequest(
    string Label,
    int MaxAttendance,
    decimal? AreaSquareMeters,
    string? Notes);
