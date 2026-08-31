namespace RaqmiSystem.Application.Sync;

/// <summary>
/// A workstation announcing that it is alive and what it is running.
/// <paramref name="StationId"/> is chosen by the client and is NOT authenticated: it says which
/// installation is speaking, never who. The user name is taken from the caller's token, not from
/// this payload, so a workstation cannot attribute its activity to somebody else.
/// </summary>
public sealed record WorkstationHeartbeatRequest(
    Guid StationId,
    string Label,
    string AppVersion,
    string? HotelUnitCode);
