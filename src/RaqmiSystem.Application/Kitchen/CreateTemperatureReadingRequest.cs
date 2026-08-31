namespace RaqmiSystem.Application.Kitchen;

/// <summary>
/// A new HACCP reading. <paramref name="MeasuredAt"/> is optional: when null the server
/// stamps the current instant - the usual case of a reading keyed in on the spot. A past
/// instant is accepted for readings transcribed from a paper log, but never a future one.
/// <paramref name="CorrectiveAction"/> is mandatory whenever the value falls outside the
/// checkpoint's range at the moment of the reading.
/// </summary>
public sealed record CreateTemperatureReadingRequest(
    string CheckpointCode,
    decimal ValueCelsius,
    DateTimeOffset? MeasuredAt,
    string? CorrectiveAction);
