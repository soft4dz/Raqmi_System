namespace RaqmiSystem.Application.Sync;

/// <summary>
/// One reported failure as shown in the journal. The workstation label is joined in so the reader
/// does not have to cross-reference identifiers by hand.
/// </summary>
public sealed record WorkstationFailureResponse(
    Guid Id,
    Guid WorkstationId,
    string WorkstationLabel,
    string Method,
    string Path,
    int? StatusCode,
    string Kind,
    string Message,
    DateTimeOffset ClaimedAtUtc,
    DateTimeOffset RecordedAtUtc,
    int ClockDriftSeconds);
