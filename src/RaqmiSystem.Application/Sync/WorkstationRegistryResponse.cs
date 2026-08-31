namespace RaqmiSystem.Application.Sync;

/// <summary>
/// The workstation registry plus the rules used to read it. The thresholds travel with the data
/// so the screen can label a workstation without hardcoding a number that would silently drift
/// away from the server's.
/// <paramref name="DistinctAppVersions"/> is the operationally useful figure of this whole
/// module: more than one version in service means clients of different builds are talking to the
/// same API, which is a real hazard rather than a cosmetic detail.
/// <paramref name="ServerTimeUtc"/> is returned so the screen can show ages against the SERVER's
/// clock instead of the reader's, which may itself be wrong.
/// </summary>
public sealed record WorkstationRegistryResponse(
    IReadOnlyCollection<WorkstationResponse> Workstations,
    int StaleAfterMinutes,
    int OfflineAfterMinutes,
    DateTimeOffset ServerTimeUtc,
    int DistinctAppVersions);
