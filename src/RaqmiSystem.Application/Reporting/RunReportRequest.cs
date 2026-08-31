namespace RaqmiSystem.Application.Reporting;

/// <summary>
/// Execution request: the report code from the catalog and its raw parameter values keyed by
/// parameter key (dates in yyyy-MM-dd). Unknown keys are refused rather than ignored, so a
/// misspelled filter can never silently widen a report.
/// </summary>
public sealed record RunReportRequest(
    string Code,
    IReadOnlyDictionary<string, string?>? Parameters);
