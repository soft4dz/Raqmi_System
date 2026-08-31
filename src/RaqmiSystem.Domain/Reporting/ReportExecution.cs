using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Reporting;

/// <summary>
/// One line of the execution journal: which report was run, with which parameters, how long it
/// took and how many rows it returned. Combined with the audit fields inherited from
/// <see cref="AuditableEntity"/> (CreatedBy/CreatedAt hold the author and the timestamp), the
/// journal answers "who pulled which figures, and when".
///
/// The RESULT itself is never stored: a report is recomputed from the live modules on every
/// run, so a stored copy would only age into a contradiction of the modules it came from.
/// </summary>
public sealed class ReportExecution : AuditableEntity
{
    public const int ReportCodeMaxLength = 60;

    public const int ParametersJsonMaxLength = 2000;

    private ReportExecution()
    {
    }

    public ReportExecution(string reportCode, string parametersJson, int rowCount, long durationMilliseconds)
    {
        ReportCode = RequireValue(reportCode, nameof(reportCode), ReportCodeMaxLength).ToLowerInvariant();
        ParametersJson = RequireValue(parametersJson, nameof(parametersJson), ParametersJsonMaxLength);

        if (rowCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rowCount), rowCount, "Row count cannot be negative.");
        }

        if (durationMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationMilliseconds), durationMilliseconds, "Duration cannot be negative.");
        }

        RowCount = rowCount;
        DurationMilliseconds = durationMilliseconds;
    }

    public string ReportCode { get; private set; } = string.Empty;

    /// <summary>
    /// The normalized parameters of the run, serialized as JSON (dates in yyyy-MM-dd, unit codes
    /// upper-cased). Stored as text on purpose: the journal is a trace to be read by a human,
    /// not a source to replay executions from.
    /// </summary>
    public string ParametersJson { get; private set; } = string.Empty;

    public int RowCount { get; private set; }

    public long DurationMilliseconds { get; private set; }

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
