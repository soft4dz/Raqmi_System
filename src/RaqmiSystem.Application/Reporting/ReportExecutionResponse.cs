namespace RaqmiSystem.Application.Reporting;

/// <summary>
/// One line of the execution journal: who ran which report, when, with which parameters, and
/// what came out (row count, duration). <see cref="ReportTitle"/> is resolved from the catalog
/// and null when the journal predates a code that has since left the catalog.
/// </summary>
public sealed record ReportExecutionResponse(
    Guid Id,
    string ReportCode,
    string? ReportTitle,
    string ParametersJson,
    string ExecutedBy,
    DateTimeOffset ExecutedAt,
    long DurationMilliseconds,
    int RowCount);
