namespace RaqmiSystem.Application.Sync;

/// <summary>
/// One workstation as shown in the registry.
/// <paramref name="MinutesSinceLastContact"/> and <paramref name="Freshness"/> are computed BY
/// THE SERVER so that every screen states the same rule instead of re-implementing the
/// thresholds - the same reason BackupStatusResponse returns IsOverdue rather than a raw age.
/// <paramref name="Freshness"/> is one of "Recent", "Stale", "Silent". None of them means
/// "online": the server never learns that a workstation was switched off.
/// </summary>
public sealed record WorkstationResponse(
    Guid Id,
    string Label,
    string LastUserName,
    string AppVersion,
    string? HotelUnitCode,
    DateTimeOffset FirstSeenUtc,
    DateTimeOffset LastSeenUtc,
    int MinutesSinceLastContact,
    string Freshness);
