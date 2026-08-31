namespace RaqmiSystem.Application.Sync;

/// <summary>
/// A batch of failures a workstation buffered while it was unable to report them one by one.
/// The batch is bounded server-side: this endpoint is reachable by any authenticated user, so it
/// must not become a way to write unbounded rows.
/// </summary>
public sealed record ReportWorkstationFailuresRequest(
    Guid StationId,
    IReadOnlyCollection<WorkstationFailureItem> Items);
