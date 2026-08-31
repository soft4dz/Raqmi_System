namespace RaqmiSystem.Application.Kitchen;

/// <summary>
/// One HACCP reading. The thresholds carried here are the SNAPSHOT frozen on the reading at
/// the moment it was recorded, not the checkpoint's current range: they are what the
/// compliance verdict was judged against, and they never change afterwards.
/// </summary>
public sealed record TemperatureReadingResponse(
    Guid Id,
    string CheckpointCode,
    string? CheckpointLabel,
    DateTimeOffset MeasuredAt,
    decimal ValueCelsius,
    string RecordedBy,
    decimal MinTempSnapshot,
    decimal MaxTempSnapshot,
    bool IsCompliant,
    string? CorrectiveAction,
    DateTimeOffset CreatedAt);
