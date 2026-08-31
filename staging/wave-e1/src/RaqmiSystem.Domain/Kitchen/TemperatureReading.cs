using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Kitchen;

/// <summary>
/// One HACCP temperature reading, append-only: a reading is a dated observation and is never
/// edited or deleted afterwards - a correction is a new reading.
///
/// Compliance is computed against the checkpoint's thresholds AT THE MOMENT of the reading
/// and FROZEN: the thresholds are copied into <see cref="MinTempSnapshot"/> /
/// <see cref="MaxTempSnapshot"/> on the reading itself, so a later threshold change never
/// rewrites the compliance history (same snapshot logic as the customer/issuer identification
/// frozen into issued invoices). Readers must render the snapshot, never the live checkpoint.
///
/// A non-compliant reading REQUIRES a corrective action: HACCP traceability is precisely the
/// record of what was done when a control failed.
/// </summary>
public sealed class TemperatureReading : AuditableEntity
{
    private TemperatureReading()
    {
    }

    public TemperatureReading(
        TemperatureCheckpoint checkpoint,
        decimal valueCelsius,
        string recordedBy,
        DateTimeOffset measuredAt,
        string? correctiveAction = null)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);

        CheckpointCode = checkpoint.Code;
        ValueCelsius = TemperatureCheckpoint.RequireCelsius(valueCelsius, nameof(valueCelsius));
        RecordedBy = RequireActor(recordedBy);
        MeasuredAt = measuredAt;

        // Freeze the thresholds the value is judged against: the compliance verdict below must
        // stay explainable forever, even after the checkpoint's range is edited.
        MinTempSnapshot = checkpoint.MinTemp;
        MaxTempSnapshot = checkpoint.MaxTemp;
        IsCompliant = checkpoint.IsWithinRange(ValueCelsius);

        var normalizedAction = NormalizeOptional(correctiveAction, nameof(correctiveAction), 500);

        if (!IsCompliant && normalizedAction is null)
        {
            throw new ArgumentException(
                "A corrective action is required for a non-compliant temperature reading.",
                nameof(correctiveAction));
        }

        CorrectiveAction = normalizedAction;
    }

    public string CheckpointCode { get; private set; } = string.Empty;

    /// <summary>Moment the temperature was observed (as opposed to CreatedAt, the moment it was keyed in).</summary>
    public DateTimeOffset MeasuredAt { get; private set; }

    public decimal ValueCelsius { get; private set; }

    public string RecordedBy { get; private set; } = string.Empty;

    /// <summary>Checkpoint lower bound at the moment of the reading - frozen, never re-read from the checkpoint.</summary>
    public decimal MinTempSnapshot { get; private set; }

    /// <summary>Checkpoint upper bound at the moment of the reading - frozen, never re-read from the checkpoint.</summary>
    public decimal MaxTempSnapshot { get; private set; }

    /// <summary>Verdict computed once against the frozen thresholds; never recomputed.</summary>
    public bool IsCompliant { get; private set; }

    /// <summary>Mandatory when the reading is non-compliant; optional note otherwise. Max 500.</summary>
    public string? CorrectiveAction { get; private set; }

    private static string RequireActor(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return "system";
        }

        return userName.Trim();
    }

    private static string? NormalizeOptional(string? value, string argumentName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", argumentName);
        }

        return trimmed;
    }
}
