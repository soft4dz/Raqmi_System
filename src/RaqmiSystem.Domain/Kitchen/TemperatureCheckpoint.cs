using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Kitchen;

/// <summary>
/// HACCP temperature checkpoint: a piece of equipment or a zone (cold room, freezer, hot
/// holding, dishwasher rinse...) with the compliance range its readings must stay within.
/// Temperatures are in Celsius, with at most one decimal place - the precision of a kitchen
/// probe thermometer; a finer value would be silently truncated by the numeric(6,1) columns.
///
/// Editing the thresholds NEVER rewrites past readings: every <see cref="TemperatureReading"/>
/// freezes the thresholds it was judged against at the moment it was recorded (same snapshot
/// logic as issued invoices).
/// </summary>
public sealed class TemperatureCheckpoint : AuditableEntity
{
    /// <summary>Sanity bounds for any Celsius value handled by this module (probe thermometers).</summary>
    public const decimal MinSupportedCelsius = -100m;

    public const decimal MaxSupportedCelsius = 500m;

    private TemperatureCheckpoint()
    {
    }

    public TemperatureCheckpoint(string code, string label, decimal minTemp, decimal maxTemp)
    {
        Code = NormalizeCode(code);
        Label = RequireValue(label, nameof(label), 200);
        SetRange(minTemp, maxTemp);
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    /// <summary>Lower compliance bound, inclusive, in Celsius.</summary>
    public decimal MinTemp { get; private set; }

    /// <summary>Upper compliance bound, inclusive, in Celsius.</summary>
    public decimal MaxTemp { get; private set; }

    public bool IsActive { get; private set; } = true;

    public void Update(string label, decimal minTemp, decimal maxTemp)
    {
        Label = RequireValue(label, nameof(label), 200);
        SetRange(minTemp, maxTemp);
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>Single source of truth for the compliance rule: both bounds are inclusive.</summary>
    public bool IsWithinRange(decimal valueCelsius)
    {
        return IsWithinRange(valueCelsius, MinTemp, MaxTemp);
    }

    /// <summary>
    /// The very same rule, applied to an explicit pair of thresholds. Two callers need it
    /// without holding the entity: a reader judging a past reading against its FROZEN snapshot
    /// (<c>TemperatureReading.MinTempSnapshot</c> / <c>MaxTempSnapshot</c>), and the desktop
    /// screen showing the verdict live during entry from a checkpoint response. Both must show
    /// the rule the server applies - referencing it here rather than recopying "&gt;= min and
    /// &lt;= max" is what keeps the screen a mirror of the domain instead of a second, drifting
    /// definition of compliance.
    /// </summary>
    public static bool IsWithinRange(decimal valueCelsius, decimal minTemp, decimal maxTemp)
    {
        return valueCelsius >= minTemp && valueCelsius <= maxTemp;
    }

    public static string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Checkpoint code is required.", nameof(code));
        }

        var normalized = code.Trim().ToUpperInvariant();

        if (normalized.Length > 40)
        {
            throw new ArgumentException("Checkpoint code cannot exceed 40 characters.", nameof(code));
        }

        return normalized;
    }

    /// <summary>
    /// Shared Celsius validation, exposed because readings apply the very same rule to the
    /// measured value: sanity bounds and at most one decimal place.
    /// </summary>
    public static decimal RequireCelsius(decimal value, string argumentName)
    {
        if (value is < MinSupportedCelsius or > MaxSupportedCelsius)
        {
            throw new ArgumentOutOfRangeException(
                argumentName,
                value,
                $"Temperature must be between {MinSupportedCelsius} and {MaxSupportedCelsius} degrees Celsius.");
        }

        if (decimal.Round(value, 1) != value)
        {
            throw new ArgumentException("Temperature cannot have more than 1 decimal place.", argumentName);
        }

        return value;
    }

    private void SetRange(decimal minTemp, decimal maxTemp)
    {
        var min = RequireCelsius(minTemp, nameof(minTemp));
        var max = RequireCelsius(maxTemp, nameof(maxTemp));

        if (min >= max)
        {
            throw new ArgumentException(
                "The minimum temperature must be strictly below the maximum temperature.",
                nameof(minTemp));
        }

        MinTemp = min;
        MaxTemp = max;
    }

    private static string RequireValue(string value, string argumentName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", argumentName);
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", argumentName);
        }

        return trimmed;
    }
}
