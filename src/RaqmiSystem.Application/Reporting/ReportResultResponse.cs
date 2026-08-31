namespace RaqmiSystem.Application.Reporting;

/// <summary>
/// The uniform result of ANY catalog report: columns, rows of raw invariant values (one value
/// per column, same order), and an optional total row highlighted by the client. One single
/// dynamic grid on the desktop renders every report from this shape.
/// </summary>
public sealed record ReportResultResponse(
    string ReportCode,
    string Title,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ReportColumnResponse> Columns,
    IReadOnlyList<IReadOnlyList<string?>> Rows,
    IReadOnlyList<string?>? TotalRow,
    int RowCount,
    long DurationMilliseconds);
